namespace Aw.Context.Core.Lenses;

/// <summary>The three bands' empty flags.</summary>
/// <param name="Intent">Records and decisions.</param>
/// <param name="Product">Own scope, neighbors and affects.</param>
/// <param name="Code">Files.</param>
public sealed record SearchedBands(BandSearched Intent, BandSearched Product, BandSearched Code);
