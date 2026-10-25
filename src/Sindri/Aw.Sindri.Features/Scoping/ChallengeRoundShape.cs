using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What 'fact:challengeRound' must be (schemas/scoping/challenge-round.json): one round, never the cumulative challenge log (#1102).</summary>
internal static class ChallengeRoundShape
{
    private const string Fact = "fact:challengeRound";
    private const string LogFix = "report only this round, as schemas/scoping/challenge-round.json: walked, verdict, surfaces, behaviours; fact:challenge is the log of earlier rounds, not your output";

    /// <summary>Whether <paramref name="fact"/> is the one this check is for.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>True for 'fact:challengeRound'.</returns>
    internal static bool Covers(FactKey fact) => string.Equals(fact.Value, Fact, StringComparison.Ordinal);

    /// <summary>The refusal when <paramref name="round"/> is a challenge log (an object with 'rounds' and no 'verdict'); null otherwise.</summary>
    /// <param name="round">The parsed fact.</param>
    /// <returns>The refusal text, or null.</returns>
    internal static string? LogShape(JsonObject round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return round["rounds"] is JsonArray && !round.ContainsKey("verdict")
            ? $"{Fact} is a challenge log, not a round (fix: {LogFix})"
            : null;
    }

    /// <summary>Why <paramref name="content"/> is not one round, in words an agent can fix; null when it is one.</summary>
    /// <param name="content">What the agent wrote.</param>
    /// <returns>The refusal text, or null.</returns>
    internal static string? Violation(ReadOnlySpan<byte> content)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(Encoding.UTF8.GetString(content));
        }
        catch (System.Text.Json.JsonException)   // boundary: the content is what an agent wrote
        {
            return null;   // the payload check says it is not JSON
        }

        if (node is not JsonObject round)
        {
            return null;
        }

        if (LogShape(round) is { } log)
        {
            return log;
        }

        var errors = ScopeSchemas.Errors("challenge-round", round);
        return errors.Count == 0
            ? null
            : $"{Fact} is not one round of schemas/scoping/challenge-round.json: {string.Join("; ", errors.Take(3).Select(static e => $"{(e.Path.Length == 0 ? "/" : e.Path)} {e.Message}"))} (fix: report only this round: walked, verdict, surfaces, behaviours)";
    }
}
