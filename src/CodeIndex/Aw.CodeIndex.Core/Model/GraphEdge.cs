namespace Aw.CodeIndex.Core.Model;

/// <summary>An edge of graphify's graph (the 'graph_edges' table).</summary>
/// <param name="Source">The source node's key.</param>
/// <param name="Target">The target node's key.</param>
/// <param name="Relation">graphify's relation word.</param>
/// <param name="Confidence">graphify's confidence word.</param>
public sealed record GraphEdge(string Source, string Target, string Relation, string Confidence);
