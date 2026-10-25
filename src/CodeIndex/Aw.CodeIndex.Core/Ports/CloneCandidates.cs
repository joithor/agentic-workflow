using Aw.CodeIndex.Core.Facade;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>What a reader finds for Similar: Core's clone logic turns the banded candidates into near clones by estimated Jaccard.</summary>
/// <param name="Exact">Symbols with the subject's shape hash.</param>
/// <param name="SubjectMinHash">The subject's MinHash, or empty when it has under 30 tokens (no clone rows).</param>
/// <param name="Banded">Symbols sharing at least one band with the subject.</param>
public sealed record CloneCandidates(IReadOnlyList<SymbolHit> Exact, ReadOnlyMemory<byte> SubjectMinHash, IReadOnlyList<MinHashCandidate> Banded);
