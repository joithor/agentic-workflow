using Aw.CodeIndex.Core.Facade;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>A symbol that shares an LSH band with the subject, with the MinHash the estimate needs.</summary>
/// <param name="Symbol">The candidate.</param>
/// <param name="MinHash">Its 64-hash MinHash.</param>
public sealed record MinHashCandidate(SymbolHit Symbol, ReadOnlyMemory<byte> MinHash);
