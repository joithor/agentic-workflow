namespace Aw.Context.Core.Lenses;

/// <summary>The three bands of a lenses view.</summary>
/// <param name="Intent">Records and decisions.</param>
/// <param name="Product">The anchor's own scope, its neighbors and what it affects.</param>
/// <param name="Code">Files that implement or test scope nodes.</param>
public sealed record LensBands(IntentBand Intent, ProductBand Product, CodeBand Code);
