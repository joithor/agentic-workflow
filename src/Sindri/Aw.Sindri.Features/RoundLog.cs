using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Features;

/// <summary>
/// A lens loop's log after one report (line spec §4.1, §5.2): the report appended to 'fact:lensRounds' (append-only, one entry a lens run, numbered by round,
/// the round's last entry marked 'closes'), the lens taken off 'fact:lensQueue', and the findings of the round so far, each with its lens.
/// </summary>
internal sealed class RoundLog
{
    private readonly LensScale _scale;
    private readonly List<JsonObject> _findings;

    private RoundLog(LensScale scale, JsonArray rounds, JsonArray queue, int round, List<JsonObject> findings)
    {
        _scale = scale;
        Rounds = rounds;
        Queue = queue;
        Round = round;
        _findings = findings;
    }

    /// <summary>The log with the report appended.</summary>
    internal JsonArray Rounds { get; }

    /// <summary>The lenses still queued.</summary>
    internal JsonArray Queue { get; }

    /// <summary>The round the report belongs to, counted from 1.</summary>
    internal int Round { get; }

    /// <summary>Whether the report was the round's last: the queue is empty.</summary>
    internal bool Ended => Queue.Count == 0;

    /// <summary>Every finding of the round so far, each with its lens.</summary>
    internal IReadOnlyList<JsonObject> Findings => _findings;

    /// <summary>The round's findings of one severity.</summary>
    /// <param name="rank">The severity's place on the scale.</param>
    /// <returns>The findings.</returns>
    internal List<JsonObject> At(int rank) => [.. _findings.Where(f => _scale.Rank(ScopeJson.Str(f, "severity")) == rank)];

    /// <summary>The round's findings that stop the loop: the most severe <see cref="LensScale.Stops"/> severities.</summary>
    /// <returns>The findings.</returns>
    internal List<JsonObject> Stopping() => [.. _findings.Where(f => _scale.Rank(ScopeJson.Str(f, "severity")) is >= 0 and < LensScale.Stops)];

    /// <summary>Appends the lens report to the log and takes its lens off the queue.</summary>
    /// <param name="scale">The loop's severity scale.</param>
    /// <param name="facts">The Step's facts: 'lensReport', 'lensRounds' and 'lensQueue'.</param>
    /// <returns>The log, or why the report cannot be logged.</returns>
    internal static Result<RoundLog> Append(LensScale scale, ScopeFacts facts)
    {
        if (facts.Unparseable.Keys.FirstOrDefault() is { } broken)
        {
            return SindriErrors.InvalidInput.With($"fact:{broken} is not JSON: {facts.Unparseable[broken]}");
        }

        if (Report(scale, facts) is not { } report)
        {
            return SindriErrors.InvalidInput.With("fact:lensReport is not a report naming its lens, with findings on the loop's scale or a notApplicable reason (fix: report {\"lens\": <id>, \"findings\": [...]})");
        }

        var lens = ScopeJson.Str(report, "lens")!;   // why: Report checked it names a lens

        var queue = facts["lensQueue"]?.DeepClone() as JsonArray ?? [];
        var rounds = facts["lensRounds"]?.DeepClone() as JsonArray ?? [];
        if (queue.FirstOrDefault(q => Names(q, lens)) is not { } queued)
        {
            return SindriErrors.InvalidInput.With($"the report is for the lens '{lens}', which fact:lensQueue does not hold (fix: run the queue's next lens)");
        }

        _ = queue.Remove(queued);
        var round = RoundOf(rounds);
        ScopeJson.Append(rounds, Entry(report, round, closes: queue.Count == 0));
        return new RoundLog(scale, rounds, queue, round, FindingsOf(rounds, round));
    }

    private static JsonObject Entry(JsonObject report, int round, bool closes)
    {
        var entry = (JsonObject)report.DeepClone();
        entry["round"] = round;
        if (closes)
        {
            entry["closes"] = true;
        }

        return entry;
    }

    // The report when it names its lens and either has findings, every one on the scale, or says why the lens does not apply.
    private static JsonObject? Report(LensScale scale, ScopeFacts facts)
    {
        if (facts["lensReport"] is not JsonObject { } report || ScopeJson.Str(report, "lens") is not { Length: > 0 })
        {
            return null;
        }

        return report["findings"] is JsonArray findings
            ? findings.All(f => f is JsonObject && scale.Rank(ScopeJson.Str(f, "severity")) >= 0) ? report : null
            : ScopeJson.Str(report, "notApplicable") is { Length: > 0 } ? report : null;
    }

    private static bool Names(JsonNode? queued, string lens) =>
        queued is JsonValue value ? value.TryGetValue<string>(out var id) && string.Equals(id, lens, StringComparison.Ordinal) : string.Equals(ScopeJson.Str(queued, "id"), lens, StringComparison.Ordinal);

    // A round continues while its last entry is open; a closed round (or no log) makes the next entry the first of a new one.
    private static int RoundOf(JsonArray rounds)
    {
        if (rounds.LastOrDefault() is not JsonObject last)
        {
            return 1;
        }

        var round = last["round"] is JsonValue value && value.TryGetValue<int>(out var n) ? n : 1;
        return last["closes"] is JsonValue closes && closes.TryGetValue<bool>(out var closed) && closed ? round + 1 : round;
    }

    private static List<JsonObject> FindingsOf(JsonArray rounds, int round) => [.. rounds.OfType<JsonObject>()
        .Where(e => e["round"] is JsonValue value && value.TryGetValue<int>(out var n) && n == round)
        .SelectMany(static e => ScopeJson.Items(e, "findings").OfType<JsonObject>().Select(f => With(f, ScopeJson.Str(e, "lens"))))];

    private static JsonObject With(JsonObject finding, string? lens)
    {
        var copy = (JsonObject)finding.DeepClone();
        copy["lens"] = lens;
        return copy;
    }

    /// <summary>The round number as text for a message.</summary>
    /// <returns>The text.</returns>
    public override string ToString() => Round.ToString(CultureInfo.InvariantCulture);
}
