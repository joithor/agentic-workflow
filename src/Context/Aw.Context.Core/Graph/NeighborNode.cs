namespace Aw.Context.Core.Graph;

/// <summary>A node near another, and how many edges away it is.</summary>
/// <param name="Node">The node.</param>
/// <param name="Depth">Edges from the center, in either direction (0 for the center).</param>
public sealed record NeighborNode(GraphNode Node, int Depth);
