namespace Aw.Context.Core.Graph;

/// <summary>One step of a path as an answer shows it.</summary>
/// <param name="Edge">The edge walked.</param>
/// <param name="Forward">True when walked from its source to its target.</param>
/// <param name="Node">The node reached.</param>
public sealed record PathFact(EdgeFact Edge, bool Forward, NodeFact Node);
