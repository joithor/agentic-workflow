namespace Aw.CodeIndex.Core.Model;

/// <summary>How far the embeddings layer is ('31 of 100 embedded').</summary>
/// <param name="Embedded">Symbols with a vector.</param>
/// <param name="Embeddable">Symbols that should have one (class-like symbols are skipped).</param>
public sealed record EmbeddingProgress(int Embedded, int Embeddable);
