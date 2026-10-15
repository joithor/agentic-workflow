using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>What a recognizer found in one file: a node, and edges from it that hold when their other end is found too.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Label">Its label.</param>
/// <param name="Confidence">0 to 1: how sure the form makes it.</param>
/// <param name="Named">True when the label came from the source text, false when it was made from the key.</param>
/// <param name="Edges">Edges from the node ('moves-to' for a keybinding's command).</param>
public sealed record Found(NodeKey Key, string Label, double Confidence, bool Named, IReadOnlyList<GraphEdge> Edges);
