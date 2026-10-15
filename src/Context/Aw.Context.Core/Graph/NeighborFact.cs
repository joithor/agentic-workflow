namespace Aw.Context.Core.Graph;

/// <summary>A neighbor as an answer shows it.</summary>
/// <param name="Node">The node.</param>
/// <param name="Depth">Edges from the center.</param>
public sealed record NeighborFact(NodeFact Node, int Depth);
