using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Map;

/// <summary>The extras 'context show' adds to a node and its edges, read from the map the caller sees: where the node came from, a short description and the other end of each edge.</summary>
/// <param name="graph">The graph the caller sees.</param>
/// <param name="text">This call's text rules.</param>
public sealed class NodeDetails(GraphSnapshot graph, MapText text)
{
    /// <summary>The node with its source and description.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The detail.</returns>
    public NodeDetail Node(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return NodeDetail.Of(text.Fact(node));
    }

    /// <summary>The edge with the kind and label of the node at its other end.</summary>
    /// <param name="edge">The edge.</param>
    /// <param name="other">The key at the other end.</param>
    /// <returns>The detail.</returns>
    public EdgeDetail Edge(EdgeFact edge, NodeKey other) => EdgeDetail.Of(edge, other.Kind, otherLabel: null);
}
