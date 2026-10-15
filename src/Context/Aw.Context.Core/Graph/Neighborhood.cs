namespace Aw.Context.Core.Graph;

/// <summary>The nodes within some edges of a center, and the edges among them.</summary>
/// <param name="Center">The center's key.</param>
/// <param name="Nodes">The nodes by depth, then key (the center first).</param>
/// <param name="Edges">Every edge whose two ends are both in <paramref name="Nodes"/>.</param>
public sealed record Neighborhood(NodeKey Center, IReadOnlyList<NeighborNode> Nodes, IReadOnlyList<GraphEdge> Edges);
