namespace Aw.Context.Core.Graph;

/// <summary>The shortest path between two nodes, edges walked in either direction.</summary>
/// <param name="From">The start.</param>
/// <param name="To">The end.</param>
/// <param name="Steps">The steps from <paramref name="From"/> to <paramref name="To"/> (empty when they are the same node).</param>
public sealed record GraphPath(NodeKey From, NodeKey To, IReadOnlyList<PathStep> Steps);
