using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>The anchors' own scopes over one graph, with the lookups the code lens shares.</summary>
internal sealed class CodeReach
{
    internal CodeReach(GraphSnapshot graph, IReadOnlyList<LensScope> scopes)
    {
        Graph = graph;
        Scopes = scopes;
        Nodes = graph.Nodes.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
        Union = scopes.SelectMany(s => s.Own).ToHashSet(StringComparer.Ordinal);
    }

    internal GraphSnapshot Graph { get; }

    internal IReadOnlyList<LensScope> Scopes { get; }

    internal IReadOnlyDictionary<string, GraphNode> Nodes { get; }

    /// <summary>The keys of every anchor's own scope.</summary>
    internal HashSet<string> Union { get; }

    /// <summary>The anchors whose own scope holds any of <paramref name="keys"/>, in the order they were given.</summary>
    internal IReadOnlyList<NodeKey> AnchorsReaching(IEnumerable<string> keys)
    {
        var wanted = keys.ToHashSet(StringComparer.Ordinal);
        return [.. Scopes.Where(s => s.Own.Overlaps(wanted)).Select(s => s.Anchor.Key)];
    }

    /// <summary>The edges of one type that end in the union scope and start at a node the graph holds.</summary>
    internal IEnumerable<GraphEdge> Into(EdgeType type) =>
        Graph.Edges.Where(e => e.Type == type && Union.Contains(e.To.Value) && Nodes.ContainsKey(e.From.Value));
}
