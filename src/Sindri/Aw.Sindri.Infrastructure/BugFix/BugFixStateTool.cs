using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.BugFix;

/// <summary>
/// <see cref="IBugFixState"/> over the bugfix-state program (AW_BUGFIX_BIN, else 'bugfix-state' on the PATH), run through <see cref="IToolRunner"/>: the one place Sindri invokes it.
/// Exit 0 carries a JSON answer; exit 3 (a rule refused) and exit 1 (an input error) carry 'refused: reason (CODE)' on stderr, which is an answer; anything else, a missing program or a timeout is unavailable.
/// </summary>
/// <param name="runner">Runs bugfix-state.</param>
/// <param name="environment">Reads AW_BUGFIX_BIN.</param>
internal sealed partial class BugFixStateTool(IToolRunner runner, Func<string, string?> environment) : IBugFixState
{
    private static readonly TimeSpan _quick = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _long = TimeSpan.FromMinutes(15);

    public ValueTask<BugFixAnswer<AttemptStarted>> StartAttempt(string stateDirectory, AttemptMode mode, CancellationToken cancellationToken)
    {
        var letter = mode == AttemptMode.Split ? "C" : "A";
        var answer = Call(["start-attempt", "--state", stateDirectory, "--mode", letter], _quick, cancellationToken)
            .Bind(json => Int(json, "attempt") is { } attempt ? BugFixAnswers.Ok(new AttemptStarted(attempt, mode)) : Unreadable<AttemptStarted>("start-attempt", "no attempt"));
        return ValueTask.FromResult(answer);
    }

    public ValueTask<BugFixAnswer<CandidateRecorded>> RecordCandidate(string stateDirectory, CandidateSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<string> arguments = ["record-candidate", "--state", stateDirectory, "--branch", source.Branch, "--cwd", source.Worktree];
        if (source.Hypothesis is { } hypothesis)
        {
            arguments.AddRange(["--hypothesis", hypothesis.ToString(CultureInfo.InvariantCulture)]);
        }

        if (source.AllowTestChanges)
        {
            arguments.Add("--allow-test-changes");
        }

        var answer = Call(arguments, _quick, cancellationToken).Bind(recorded =>
            Text(recorded, "candidate") is { } candidate && Text(recorded, "commit") is { } commit
                ? Call(["resume", "--state", stateDirectory], _quick, cancellationToken).Bind(resume =>
                    Int(resume, "attempt") is { } attempt
                        ? BugFixAnswers.Ok(new CandidateRecorded(candidate, commit, attempt, Strings(recorded, "changedFiles")))
                        : Unreadable<CandidateRecorded>("resume", "no attempt"))
                : Unreadable<CandidateRecorded>("record-candidate", "no candidate or commit"));
        return ValueTask.FromResult(answer);
    }

    public ValueTask<BugFixAnswer<CheckRun>> Recheck(string stateDirectory, string candidate, string worktree, FrozenCheck check, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(check);
        var directory = string.IsNullOrEmpty(check.Directory) || string.Equals(check.Directory, ".", StringComparison.Ordinal) ? worktree : Path.Combine(worktree, check.Directory);
        List<string> run = string.Equals(check.Kind, "ui", StringComparison.Ordinal)
            ? ["run-ui", "--state", stateDirectory, "--check", check.File, "--cwd", directory]
            : ["run-test", "--state", stateDirectory, "--check", check.File, "--cwd", directory, "--", .. check.Command];
        var answer = Call(run, _long, cancellationToken).Bind(ran =>
        {
            if (Text(ran, "evidence") is not { } evidence)
            {
                return Unreadable<CheckRun>(run[0], "no evidence");
            }

            var exitCode = Int(ran, "exitCode") ?? 0;
            return Call(["record-run", candidate, "--state", stateDirectory, "--evidence", evidence], _quick, cancellationToken).Bind(recorded =>
                recorded["passed"] is JsonValue { } flag && flag.TryGetValue<bool>(out var passed)
                    ? BugFixAnswers.Ok(new CheckRun(candidate, passed, exitCode, evidence))
                    : Unreadable<CheckRun>("record-run", "no passed flag"));
        });
        return ValueTask.FromResult(answer);
    }

    public ValueTask<BugFixAnswer<ResolutionVerdict>> Judge(string stateDirectory, string candidate, CancellationToken cancellationToken)
    {
        var answer = Call(["judge", candidate, "--state", stateDirectory], _long, cancellationToken).Bind(json =>
            Decision(Text(json, "decision")) is { } decision
                ? BugFixAnswers.Ok(new ResolutionVerdict(Text(json, "candidate") ?? candidate, decision, Text(json, "reasonCode") ?? string.Empty, Strings(json, "reasons")))
                : Unreadable<ResolutionVerdict>("judge", "no decision"));
        return ValueTask.FromResult(answer);
    }

    [GeneratedRegex(@"^(?:refused|error): (?<message>.*) \((?<code>AW-BUGFIX-\d+)\)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RefusalLine { get; }

    private static JudgeDecision? Decision(string? name) => name switch
    {
        "resolved" => JudgeDecision.Resolved,
        "partial" => JudgeDecision.Partial,
        "unresolved" => JudgeDecision.Unresolved,
        "escalated" => JudgeDecision.Escalated,
        _ => null,
    };

    private static BugFixAnswer<T> Unreadable<T>(string verb, string why) => BugFixAnswers.Failed<T>($"bugfix-state {verb} printed an unreadable answer: {why}");

    private static JsonObject? Parse(string output)
    {
        try
        {
            return JsonNode.Parse(output) as JsonObject;
        }
        catch (JsonException)
        {
            return null;   // boundary: the tool printed something else
        }
    }

    private static string? Text(JsonObject json, string name) => json[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int? Int(JsonObject json, string name) => json[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    private static List<string> Strings(JsonObject json, string name) => json[name] is JsonArray items
        ? [.. items.OfType<JsonValue>().Select(static i => i.TryGetValue<string>(out var text) ? text : null).OfType<string>()]
        : [];

    private static string Tail(string text) => text.Trim() is { Length: > 300 } trimmed ? trimmed[^300..] : text.Trim();

    private BugFixAnswer<JsonObject> Call(List<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var program = environment("AW_BUGFIX_BIN") is { Length: > 0 } given ? given : "bugfix-state";
        var run = runner.Run(program, arguments, timeout, options: null, cancellationToken);
        if (run.Outcome != ToolOutcome.Exited)
        {
            return Unavailable($"{arguments[0]} ({program}: {run.Outcome})");
        }

        if (run.ExitCode == 0)
        {
            return Parse(run.Output) is { } json ? BugFixAnswers.Ok(json) : Unavailable($"{arguments[0]} ({program}: it printed no JSON: {Tail(run.Output)})");
        }

        // A rule refused (exit 3) or the input was wrong (exit 1): the line names the code. Usage (exit 2) and anything unparseable are not answers.
        if (run.ExitCode is 1 or 3 && Refusal(run.Error) is { } refusal)
        {
            return BugFixAnswers.Refused<JsonObject>(refusal);
        }

        return Unavailable($"{arguments[0]} ({program}: exit {run.ExitCode.ToString(CultureInfo.InvariantCulture)}: {Tail(run.Error.Length > 0 ? run.Error : run.Output)})");
    }

    private static BugFixRefusal? Refusal(string stderr)
    {
        var line = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (line is null || RefusalLine.Match(line) is not { Success: true } match)
        {
            return null;
        }

        var code = match.Groups["code"].Value;
        var message = match.Groups["message"].Value;
        var kind = code switch
        {
            "AW-BUGFIX-010" when message.Contains("attempt cap", StringComparison.Ordinal) => RefusalKind.AttemptCap,
            "AW-BUGFIX-015" when message.Contains("no passing run", StringComparison.Ordinal) => RefusalKind.NoPassingRun,
            _ => RefusalKind.Other,
        };
        return new BugFixRefusal(code, kind, message);
    }

    private static BugFixAnswer<JsonObject> Unavailable(string what) =>
        BugFixAnswers.Failed<JsonObject>($"bugfix-state {what} failed (fix: build bugfix-state and put it on the PATH, or set AW_BUGFIX_BIN)");
}
