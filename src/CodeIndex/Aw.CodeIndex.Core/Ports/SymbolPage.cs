using Aw.CodeIndex.Core.Facade;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>A page of symbol hits from a reader.</summary>
/// <param name="Hits">The hits, ordered.</param>
/// <param name="Truncated">True when more matched than were returned.</param>
public sealed record SymbolPage(IReadOnlyList<SymbolHit> Hits, bool Truncated);
