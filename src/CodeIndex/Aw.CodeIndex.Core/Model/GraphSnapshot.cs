namespace Aw.CodeIndex.Core.Model;

/// <summary>What a graph run returned, already checked against the snapshot's file list and the node and edge caps.</summary>
/// <param name="Nodes">The nodes.</param>
/// <param name="Edges">The edges.</param>
public sealed record GraphSnapshot(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges);
