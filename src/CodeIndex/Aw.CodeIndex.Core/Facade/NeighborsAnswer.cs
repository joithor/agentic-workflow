namespace Aw.CodeIndex.Core.Facade;

/// <summary>The answer to Neighbors.</summary>
/// <param name="Stamp">Which index answered; its layer is embeddings.</param>
/// <param name="Neighbors">The k nearest, nearest first.</param>
public sealed record NeighborsAnswer(AnswerStamp Stamp, IReadOnlyList<Neighbor> Neighbors);
