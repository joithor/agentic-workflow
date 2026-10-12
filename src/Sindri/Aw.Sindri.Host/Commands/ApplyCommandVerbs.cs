using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.ApplyCommand;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow answer &lt;address&gt; [--text …|--file …] [--grant n | --grant-tokens n | --accept | --reject |
/// --resume]', 'workflow command &lt;address&gt; &lt;verb&gt;' and 'workflow cancel &lt;address&gt;', each with '[--nonce
/// &lt;n&gt;] [--json]' (engine spec rev 8 §5.2, §5.8, §6.3; engine P4 unit (b)). Each calls the ApplyCommand slice and
/// prints the new status header and what happened (an answer: the resume action it ran); a repeated nonce prints
/// 'already applied (…)'. A protected answer (grant, grant-tokens, accept) shows what it does, asks the server for its
/// challenge, and reads the action text typed at the terminal (§4.2). Exit 0 applied, 2 refused, 1 invalid or not running.
/// </summary>
internal static class ApplyCommandVerbs
{
    private const string AnswerUsage =
        "usage: sindri workflow answer <address> [--text <t> | --file <f>] [--grant <n> | --grant-tokens <n> | --accept | --reject | --resume] [--nonce <n>] [--json]";

    private const string CommandUsage = "usage: sindri workflow command <address> <verb> [--nonce <n>] [--json]";

    private const string CancelUsage = "usage: sindri workflow cancel <address> [--nonce <n>] [--json]";

    /// <summary>The answer options that take no value.</summary>
    private static readonly string[] _switches = ["--accept", "--reject", "--resume"];

    /// <summary>The answer options that take one value.</summary>
    private static readonly string[] _valued = ["--text", "--file", "--grant", "--grant-tokens", "--nonce"];

    /// <summary>The answer options that carry text or the nonce rather than choose the variant.</summary>
    private static readonly string[] _carriers = ["--text", "--file", "--nonce"];

    /// <summary>The actor a terminal's command carries until API P1's caller chain names it (spec §6.2).</summary>
    private const string Actor = "human:cli";

    internal static async Task<int> Answer(string[] args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var (json, rest) = Json(args);
        if (rest is not [var address, .. var options])
        {
            return await output.Error(SindriErrors.InvalidInput.With(AnswerUsage)).ConfigureAwait(false);
        }

        var parsed = await AnswerOptions(options, ct).ConfigureAwait(false);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var (variant, text, amount, nonce) = parsed.Value;
        var request = new ApplyCommandRequest(address, ApplyCommandKind.Answer, variant, text, amount, Verb: null, nonce, Challenge: null, Actor);
        if (ApplyCommandChallenge.IsProtected(request.Kind, variant))
        {
            var challenge = await Challenge(request, services, output, ct).ConfigureAwait(false);
            if (challenge.IsError)
            {
                return await output.Error(challenge.Error).ConfigureAwait(false);
            }

            request = request with { Challenge = challenge.Value };
        }

        return await Apply(request, json, services, output, ct).ConfigureAwait(false);
    }

    internal static async Task<int> Command(string[] args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var (json, rest) = Json(args);
        if (rest is not [var address, var verb, .. var options] || !CommandVerb.TryFrom(verb, out var parsed))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"the verb matches [a-z][a-z-]{{1,31}} ({CommandUsage})")).ConfigureAwait(false);
        }

        var nonce = Nonce(options, CommandUsage);
        return nonce.IsError
            ? await output.Error(nonce.Error).ConfigureAwait(false)
            : await Apply(new ApplyCommandRequest(address, ApplyCommandKind.Command, Answer: null, Text: null, Amount: null, parsed, nonce.Value, Challenge: null, Actor), json, services, output, ct).ConfigureAwait(false);
    }

    internal static async Task<int> Cancel(string[] args, IServiceProvider services, Output output, CancellationToken ct)
    {
        var (json, rest) = Json(args);
        if (rest is not [var address, .. var options])
        {
            return await output.Error(SindriErrors.InvalidInput.With(CancelUsage)).ConfigureAwait(false);
        }

        var nonce = Nonce(options, CancelUsage);
        return nonce.IsError
            ? await output.Error(nonce.Error).ConfigureAwait(false)
            : await Apply(new ApplyCommandRequest(address, ApplyCommandKind.Cancel, Answer: null, Text: null, Amount: null, Verb: null, nonce.Value, Challenge: null, Actor), json, services, output, ct).ConfigureAwait(false);
    }

    private static async Task<int> Apply(ApplyCommandRequest request, bool json, IServiceProvider services, Output output, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var done = await scope.ServiceProvider.GetRequiredService<IService<ApplyCommandRequest, ApplyCommandResponse>>().Handle(request, ct).ConfigureAwait(false);
        if (done.IsError)
        {
            return await output.Error(done.Error).ConfigureAwait(false);
        }

        if (json)
        {
            return await output.Json(done.Value, 0).ConfigureAwait(false);
        }

        var happened = done.Value.AlreadyApplied ? $"already applied ({done.Value.Result.Happened})" : done.Value.Result.Happened;
        var header = InstanceHeader.Line(done.Value.Result.Instance, services.GetRequiredService<TimeProvider>().GetUtcNow());
        return await output.Text($"{header}\n  {happened}\n", 0).ConfigureAwait(false);
    }

    /// <summary>
    /// The §4.2 challenge of a protected answer: the instance as it is now, what the answer does, and the action text the
    /// human types back at the terminal. The server checks the typed text and the subject when the answer arrives.
    /// </summary>
    private static async Task<Result<ChallengeAnswer>> Challenge(ApplyCommandRequest request, IServiceProvider services, Output output, CancellationToken ct)
    {
        var address = await services.GetRequiredService<IInstanceDirectory>().Resolve(request.Instance, ct).ConfigureAwait(false);
        if (address.IsError)
        {
            return address.Error;
        }

        var view = await services.GetRequiredService<IInstanceRuntime>().Get(address.Value, ct).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var action = ApplyCommandChallenge.Action(address.Value, request.Answer!.Value, request.Amount); // why: only a protected answer asks
        var issued = services.GetRequiredService<IChallenges>().Issue(action, ApplyCommandChallenge.Subject(view.Value));
        var header = InstanceHeader.Line(view.Value, services.GetRequiredService<TimeProvider>().GetUtcNow());
        var question = view.Value.Question is { } q ? $"\n  {q.Question}" : string.Empty;
        await output.Out.WriteAsync($"{header}{question}\n  type '{action.Text}' to confirm: ").ConfigureAwait(false);
        await output.Out.FlushAsync(ct).ConfigureAwait(false);
        var typed = (await Console.In.ReadLineAsync(ct).ConfigureAwait(false))?.Trim() ?? string.Empty;
        return new ChallengeAnswer(issued.Id, typed);
    }

    /// <summary>The answer's variant and its text or amount, and the optional nonce; exactly one variant, '--resume' may carry '--text'.</summary>
    private static async Task<Result<(AnswerVariant Variant, string? Text, int? Amount, CommandNonce? Nonce)>> AnswerOptions(string[] options, CancellationToken ct)
    {
        var tokens = Tokens(options);
        if (tokens.IsError)
        {
            return tokens.Error;
        }

        string? Last(string flag) => tokens.Value.LastOrDefault(t => t.Flag == flag).Value;
        var text = Last("--file") is { } file ? await ReadText(file, ct).ConfigureAwait(false) : Last("--text");
        if (text.IsError)
        {
            return text.Error;
        }

        var variant = Variant(tokens.Value, text.Value is not null);
        if (variant.IsError)
        {
            return variant.Error;
        }

        if (Last("--nonce") is not { } nonce)
        {
            return (variant.Value.Variant, text.Value, variant.Value.Amount, null);
        }

        return CommandNonce.TryFrom(nonce, out var parsed)
            ? (variant.Value.Variant, text.Value, variant.Value.Amount, parsed)
            : SindriErrors.InvalidInput.With($"'{nonce}' isn't a nonce: 32 lower-case hex digits ({AnswerUsage})");
    }

    /// <summary>The options as '(flag, value)' pairs: '--accept', '--reject' and '--resume' take no value, the others one.</summary>
    private static Result<List<(string Flag, string? Value)>> Tokens(string[] options)
    {
        var tokens = new List<(string Flag, string? Value)>();
        for (var i = 0; i < options.Length; i++)
        {
            var flag = options[i];
            if (_switches.Contains(flag, StringComparer.Ordinal))
            {
                tokens.Add((flag, null));
            }
            else if (_valued.Contains(flag, StringComparer.Ordinal) && i + 1 < options.Length)
            {
                tokens.Add((flag, options[++i]));
            }
            else
            {
                return SindriErrors.InvalidInput.With($"'{flag}' isn't an option here, or needs a value ({AnswerUsage})");
            }
        }

        return tokens;
    }

    /// <summary>Exactly one variant: a grant (with its amount), '--accept', '--reject', '--resume' (which may carry the text), or the text alone.</summary>
    private static Result<(AnswerVariant Variant, int? Amount)> Variant(List<(string Flag, string? Value)> tokens, bool hasText)
    {
        var given = tokens.Where(static t => !_carriers.Contains(t.Flag, StringComparer.Ordinal)).ToList();
        var resumeWithText = given is [("--resume", _)];
        return (given, hasText) switch
        {
            ([], true) => (AnswerVariant.Text, null),
            ([("--accept", _)], false) => (AnswerVariant.Accept, null),
            ([("--reject", _)], false) => (AnswerVariant.Reject, null),
            ([("--resume", _)], _) when resumeWithText => (AnswerVariant.Resume, null),
            ([(var flag and ("--grant" or "--grant-tokens"), { } n)], false) when Amount(n, flag) is { } amount =>
                (flag == "--grant" ? AnswerVariant.Grant : AnswerVariant.GrantTokens, amount),
            _ => SindriErrors.InvalidInput.With($"give exactly one of --text/--file, --grant <n>, --grant-tokens <n>, --accept, --reject or --resume [--text] ({AnswerUsage})"),
        };
    }

    /// <summary>'--grant n' is a whole number; '--grant-tokens n' also takes '100k' or '1.5m'.</summary>
    private static int? Amount(string text, string flag) =>
        flag == "--grant"
            ? int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var rounds) ? rounds : null
            : TokenCounts.TryParse(text, out var tokens) ? tokens : null;

    private static async Task<Result<string?>> ReadText(string file, CancellationToken ct)
    {
        try
        {
            var info = new FileInfo(file);
            if (info.Exists && info.Length > IFactStore.MaxBytes)
            {
                return SindriErrors.InvalidInput.With($"{file} is larger than 1 MiB (fix: answer with a smaller file)");
            }

            return await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unreadable file is the caller's input error
        {
            return SindriErrors.InvalidInput.With($"{file} can't be read: {e.Message}");
        }
    }

    /// <summary>The optional '--nonce &lt;n&gt;', the only option the command and cancel verbs take.</summary>
    private static Result<CommandNonce?> Nonce(string[] options, string usage) => options switch
    {
        [] => (CommandNonce?)null,
        ["--nonce", var given] when CommandNonce.TryFrom(given, out var nonce) => nonce,
        ["--nonce", var given] => SindriErrors.InvalidInput.With($"'{given}' isn't a nonce: 32 lower-case hex digits ({usage})"),
        _ => SindriErrors.InvalidInput.With($"'{string.Join(' ', options)}' isn't an option here ({usage})"),
    };

    private static (bool Json, string[] Others) Json(string[] args) =>
        (args.Contains("--json", StringComparer.Ordinal), [.. args.Where(static a => a != "--json")]);
}
