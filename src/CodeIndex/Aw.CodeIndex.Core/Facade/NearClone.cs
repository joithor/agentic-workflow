namespace Aw.CodeIndex.Core.Facade;

/// <summary>A near clone: found through the MinHash bands, kept when its estimated Jaccard similarity reaches the threshold.</summary>
/// <param name="Symbol">The clone.</param>
/// <param name="Similarity">The estimated Jaccard similarity.</param>
public sealed record NearClone(SymbolHit Symbol, double Similarity);
