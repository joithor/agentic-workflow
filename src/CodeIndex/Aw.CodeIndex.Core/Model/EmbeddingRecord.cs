using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A symbol's vector (the 'embeddings' table).</summary>
/// <param name="Symbol">The symbol.</param>
/// <param name="Model">The model that made it.</param>
/// <param name="Vector">The vector.</param>
public sealed record EmbeddingRecord(SymbolRef Symbol, string Model, ReadOnlyMemory<float> Vector);
