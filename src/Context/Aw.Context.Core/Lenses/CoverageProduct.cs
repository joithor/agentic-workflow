namespace Aw.Context.Core.Lenses;

/// <summary>Scope nodes a human or the extractor stands behind.</summary>
/// <param name="Confirmed">Confirmed nodes.</param>
/// <param name="Of">Scope size.</param>
public sealed record CoverageProduct(int Confirmed, int Of);
