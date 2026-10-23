using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.WholeMap;

/// <summary>A graph written as keys and edges, for the area rules.</summary>
internal sealed class AreaGraph
{
    private readonly List<GraphNode> _nodes = [];
    private readonly List<GraphEdge> _edges = [];

    public AreaGraph Node(params string[] keys)
    {
        _nodes.AddRange(keys.Select(k => new GraphNode(NodeKey.From(k), k)));
        return this;
    }

    public AreaGraph Edge(string from, EdgeType type, string to)
    {
        _edges.Add(new GraphEdge(NodeKey.From(from), NodeKey.From(to), type));
        return this;
    }

    public GraphSnapshot Build(bool reversed = false) =>
        new(reversed ? [.. _nodes.AsEnumerable().Reverse()] : _nodes, reversed ? [.. _edges.AsEnumerable().Reverse()] : _edges, [], []);
}
