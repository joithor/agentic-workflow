namespace Aw.Context.Core.Lenses;

/// <summary>Builds <see cref="LensSearched"/>.</summary>
internal static class LensSearchedBuilder
{
    internal static LensSearched Placeholder { get; } = new(Anchors: [], MapVersion: string.Empty, Depth: 0, Hops: [], Sources: [], Bands: new SearchedBands(Intent: new BandSearched(Empty: true), Product: new BandSearched(Empty: true), Code: new BandSearched(Empty: true)));
}
