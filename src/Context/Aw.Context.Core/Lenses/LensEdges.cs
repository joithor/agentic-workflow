using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Edges as the lenses show them.</summary>
internal static class LensEdges
{
    private static readonly EdgeType[] _across = [EdgeType.Mentions, EdgeType.Decides, EdgeType.Quotes, EdgeType.Supersedes, EdgeType.Implements, EdgeType.Tests];

    /// <summary>One edge, with its id, provenance and whether anyone stands behind it.</summary>
    internal static LensEdge Of(GraphEdge e) => new(e.From, e.To, e.Type, e.Provenance, LinkIds.For(e), LensScope.Confirmed(e.Provenance), e.Confidence);

    /// <summary>The edges across the bands (mentions, decides, quotes, supersedes, implements, tests) among the items.</summary>
    internal static List<LensEdge> Across(GraphSnapshot graph, HashSet<string> items) =>
        [.. graph.Edges.Where(e => _across.Contains(e.Type) && items.Contains(e.From.Value) && items.Contains(e.To.Value))
            .OrderBy(e => e.Type).ThenBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).Select(Of)];
}
