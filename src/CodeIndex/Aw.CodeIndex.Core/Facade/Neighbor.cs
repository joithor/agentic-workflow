namespace Aw.CodeIndex.Core.Facade;

/// <summary>A symbol near a text by embedding.</summary>
/// <param name="Symbol">The symbol.</param>
/// <param name="Score">The similarity score, higher is nearer.</param>
public sealed record Neighbor(SymbolHit Symbol, double Score);
