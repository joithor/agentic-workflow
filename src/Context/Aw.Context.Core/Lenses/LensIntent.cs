using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>The intent band (spec §8.9): the records that mention scope nodes and the decisions that decide them, read from stored edges only.</summary>
internal static class LensIntent
{
    /// <summary>Builds the band.</summary>
    /// <param name="scope">The anchor's scope.</param>
    /// <returns>The band, each list cut at <see cref="LensesBuilder.BandCap"/>.</returns>
    internal static IntentBand Build(LensScope scope)
    {
        var records = Records(scope);
        var decisions = Decisions(scope);
        var cut = records.Count > LensesBuilder.BandCap || decisions.Count > LensesBuilder.BandCap;
        return new IntentBand([.. records.Take(LensesBuilder.BandCap)], [.. decisions.Take(LensesBuilder.BandCap)], new IntentCounts(records.Count, decisions.Count, Orphaned: null), cut);
    }

    /// <summary>Where a record lives, from its key: 'linear', 'linear:doc', 'slack', 'onyx' or 'repo-doc'.</summary>
    internal static string Source(NodeKey record)
    {
        var key = record.Name;
        var colon = key.IndexOf(':', StringComparison.Ordinal);
        var scheme = colon < 0 ? key : key[..colon];
        return scheme switch
        {
            "linear" => key[(colon + 1)..].StartsWith("doc/", StringComparison.Ordinal) ? "linear:doc" : "linear",
            "repo-docs" or "repo-doc" => "repo-doc",
            _ => scheme,
        };
    }

    internal static List<LensRecord> Records(LensScope scope) =>
        [.. scope.IntoScope(EdgeType.Mentions).Where(e => scope.Nodes.ContainsKey(e.From.Value))
            .GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new LensRecord(scope.Nodes[g.Key].Key, scope.Nodes[g.Key].Label, Source(scope.Nodes[g.Key].Key),
                [.. g.Select(e => e.To).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)],
                Results.Trust.Untrusted, CitationState.Unchecked, [], NodeKind.Record))];

    internal static List<LensDecision> Decisions(LensScope scope) =>
        [.. scope.IntoScope(EdgeType.Decides).Where(e => scope.Nodes.ContainsKey(e.From.Value))
            .GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => Decision(scope, scope.Nodes[g.Key], [.. g.Select(e => e.To).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)]))];

    private static LensDecision Decision(LensScope scope, GraphNode node, IReadOnlyList<NodeKey> decides)
    {
        var row = scope.Graph.Decisions.FirstOrDefault(d => d.ToKey().Value == node.Key.Value);
        var supersedes = scope.Graph.Edges.Where(e => e.Type == EdgeType.Supersedes && e.From.Value == node.Key.Value)
            .Select(e => e.To).OrderBy(k => k.Value, StringComparer.Ordinal).ToList();
        var confirmed = LensScope.Confirmed(node.Provenance);
        return new LensDecision(node.Key, row?.Wording ?? node.Label, decides, supersedes, Quotes: null, node.Provenance, confirmed, TrustPolicy.For(node.Provenance, confirmed),
            row?.At ?? node.At, row is null ? null : Quote(row), node.Label, NodeKind.Decision);
    }

    private static LensQuote Quote(DecisionRow row) => new(row.Record, row.Quote, Results.Trust.Untrusted);
}
