namespace Aw.Context.Core.Lenses;

/// <summary>What the file map was read from.</summary>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Sha">The commit the tree was listed at.</param>
public sealed record FileMapBasis(string MapVersion, string Sha);
