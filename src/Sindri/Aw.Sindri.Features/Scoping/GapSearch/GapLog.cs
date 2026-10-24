using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What one gap-log round decided: the outcome, the cumulative record, the anchors to look at again and the facts only one outcome writes.</summary>
/// <param name="Outcome">'clear' or 'confirmed'.</param>
/// <param name="GapSearch">The cumulative record with this round added.</param>
/// <param name="GapAnchors">The anchors of the confirmed gaps; empty on 'clear'.</param>
/// <param name="Findings">The findings fact with one SCP-GAP-001 finding per confirmed gap; null on 'clear'.</param>
/// <param name="Closed">The latch, {"round": n}; null on 'confirmed'.</param>
internal sealed record GapLogResult(string Outcome, JsonObject GapSearch, JsonArray GapAnchors, JsonArray? Findings, JsonObject? Closed);

/// <summary>The gap log's rule (spec 4.6), pure: pairs the searcher's candidates with the judge's rulings and appends the round.</summary>
internal static class GapLog
{
    /// <summary>Appends a round.</summary>
    /// <param name="gapSearch">The cumulative record so far.</param>
    /// <param name="candidates">The searcher's fact.</param>
    /// <param name="rulings">The judge's fact.</param>
    /// <param name="findings">The findings fact so far.</param>
    /// <returns>The round, or the reason the Step fails.</returns>
    internal static Result<GapLogResult> Apply(JsonNode? gapSearch, JsonNode? candidates, JsonNode? rulings, JsonNode? findings)
    {
        _ = (gapSearch, candidates, rulings, findings);
        throw new NotSupportedException();
    }
}
