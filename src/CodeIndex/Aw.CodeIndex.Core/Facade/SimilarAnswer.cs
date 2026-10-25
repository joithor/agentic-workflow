using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>The answer to Similar.</summary>
/// <param name="Stamp">Which index answered; its layer is clones.</param>
/// <param name="Subject">The symbol asked about.</param>
/// <param name="Exact">Symbols with the same shape hash.</param>
/// <param name="Near">Symbols over the similarity threshold.</param>
public sealed record SimilarAnswer(AnswerStamp Stamp, SymbolRef Subject, IReadOnlyList<SymbolHit> Exact, IReadOnlyList<NearClone> Near);
