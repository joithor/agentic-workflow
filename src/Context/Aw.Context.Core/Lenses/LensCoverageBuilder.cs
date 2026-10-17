using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Counts how well an anchor's own scope is backed (spec §8.9). One function serves the lenses and 'context map status --anchor', so their numbers agree.</summary>
public static class LensCoverageBuilder
{
    private const string FileLevel = "file";

    /// <summary>The coverage of <paramref name="anchor"/>'s own scope.</summary>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="anchor">The anchor node (a key or an alias); an unknown one has an empty scope.</param>
    /// <returns>The coverage.</returns>
    public static LensCoverage ForAnchor(GraphSnapshot graph, NodeKey anchor)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return LensScope.Of(graph, anchor, depth: 0) is { } scope ? Of(scope) : new LensCoverage(0, new CoverageIntent(0, 0), new CoverageProduct(0, 0), new CoverageCode(0, 0, FileLevel, 0), []);
    }

    internal static LensCoverage Of(LensScope scope) => Of(scope.Graph, scope.Nodes, scope.Cascade.OwnScope);

    /// <summary>The coverage of any own scope (one anchor's, or the union of several).</summary>
    internal static LensCoverage Of(GraphSnapshot graph, IReadOnlyDictionary<string, GraphNode> nodes, IReadOnlyCollection<NodeKey> ownScope)
    {
        var order = ownScope.OrderBy(k => k.Value, StringComparer.Ordinal).ToList();
        var own = ownScope.Select(k => k.Value).ToHashSet(StringComparer.Ordinal);
        IEnumerable<GraphEdge> Into(params EdgeType[] types) => graph.Edges.Where(e => types.Contains(e.Type) && own.Contains(e.To.Value));
        var covered = Into(EdgeType.Mentions, EdgeType.Decides).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        var implements = Into(EdgeType.Implements).ToList();
        var confirmed = implements.Where(e => LensScope.Confirmed(e.Provenance)).Select(e => e.To.Value).ToHashSet(StringComparer.Ordinal);
        var suggested = implements.Select(e => e.To.Value).Where(t => !confirmed.Contains(t)).ToHashSet(StringComparer.Ordinal);
        var product = order.Count(k => nodes.TryGetValue(k.Value, out var n) && LensScope.Confirmed(n.Provenance));
        return new LensCoverage(order.Count, new CoverageIntent(order.Count(k => covered.Contains(k.Value)), order.Count),
            new CoverageProduct(product, order.Count), new CoverageCode(order.Count(k => confirmed.Contains(k.Value)), order.Count, FileLevel, order.Count(k => suggested.Contains(k.Value))),
            [.. order.Select(k => Gap(k, covered.Contains(k.Value), confirmed.Contains(k.Value), suggested.Contains(k.Value))).OfType<LensGap>()]);
    }

    private static LensGap? Gap(NodeKey node, bool intent, bool code, bool suggested)
    {
        if (intent && code)
        {
            return null;
        }

        GapKind[] missing = (intent, code) switch
        {
            (false, false) => [GapKind.Intent, GapKind.Code],
            (false, true) => [GapKind.Intent],
            _ => [GapKind.Code],
        };
        var note = (intent, code, suggested) switch
        {
            (true, false, true) => "has intent; its only implements link is a suggestion",
            (true, false, false) => "has intent, no implements",
            (false, true, _) => "implemented, no record or decision",
            _ => null,
        };
        return new LensGap(node, missing, note);
    }
}
