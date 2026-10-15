namespace Aw.Context.Core.Graph;

/// <summary>One step of a path: the edge walked and the node it reaches.</summary>
/// <param name="Edge">The edge.</param>
/// <param name="Forward">True when the edge points from the previous node to <paramref name="Node"/>; false when it was walked backwards.</param>
/// <param name="Node">The node reached.</param>
public sealed record PathStep(GraphEdge Edge, bool Forward, NodeKey Node);
