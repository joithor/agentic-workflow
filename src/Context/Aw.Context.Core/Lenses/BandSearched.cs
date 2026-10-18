namespace Aw.Context.Core.Lenses;

/// <summary>Whether a band came back with nothing, so an empty band reads as searched-and-empty.</summary>
/// <param name="Empty">True when the band lists no item.</param>
public sealed record BandSearched(bool Empty);
