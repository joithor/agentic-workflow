using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>The code band (spec §8.9): one entry per file that implements or tests a scope node. File level until the code index exists (Phase 9).</summary>
internal static class LensCode
{
    /// <summary>Builds the band.</summary>
    /// <param name="scope">The anchor's scope.</param>
    /// <returns>The band (at most 100 files) and the keys of all its files.</returns>
    internal static (CodeBand Band, HashSet<string> Keys) Build(LensScope scope)
    {
        var files = scope.IntoScope(EdgeType.Implements, EdgeType.Tests).Where(e => scope.Nodes.ContainsKey(e.From.Value))
            .GroupBy(e => e.From.Value, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(File).ToList();
        var noCode = scope.Cascade.OwnScope.Count(k => k.Kind == NodeKind.Feature && !Implemented(scope.Graph).Contains(k.Value));
        var band = new CodeBand([.. files.Take(LensesBuilder.BandCap)], LensSymbols.Unavailable, new CodeCounts(files.Count, 0, noCode), files.Count > LensesBuilder.BandCap);
        return (band, scope.IntoScope(EdgeType.Implements, EdgeType.Tests).Select(e => e.From.Value).ToHashSet(StringComparer.Ordinal));
    }

    private static HashSet<string> Implemented(GraphSnapshot graph) =>
        graph.Edges.Where(e => e.Type == EdgeType.Implements && LensScope.Confirmed(e.Provenance)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);

    private static LensFile File(IGrouping<string, GraphEdge> group)
    {
        var edges = group.ToList();
        var best = edges.OrderByDescending(e => LensScope.Confirmed(e.Provenance)).ThenByDescending(e => e.Confidence).ThenBy(e => e.To.Value, StringComparer.Ordinal).First();
        var confirmed = edges.Exists(e => LensScope.Confirmed(e.Provenance));
        return new LensFile(best.From.Name, Targets(edges, EdgeType.Implements), Targets(edges, EdgeType.Tests), best.Provenance, best.Confidence, confirmed, Suggested: !confirmed,
            confirmed ? null : LinkIds.For(best), string.Empty, NodeKind.Code, Results.Trust.Trusted);
    }

    private static IReadOnlyList<NodeKey> Targets(List<GraphEdge> edges, EdgeType type) =>
        [.. edges.Where(e => e.Type == type).Select(e => e.To).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];
}
