using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds <see cref="LensSearched"/> (scoping spec C6): what the answer rests on and which bands came back empty.</summary>
internal static class LensSearchedBuilder
{
    private static readonly (CascadeHops Hop, string Name)[] _hops =
        [(CascadeHops.State, "state"), (CascadeHops.Dependency, "dependency"), (CascadeHops.Flow, "flow"), (CascadeHops.Component, "component")];

    internal static LensSearched For(LensBasis basis, IReadOnlyList<string> anchors, int depth, LensBands bands)
    {
        var sources = new List<SearchedSource> { SearchedSources.Map(basis.MapCreated ?? basis.AsOf) };
        sources.AddRange(basis.IntentSources);
        sources.Add(SearchedSources.CodeIndex);
        var intent = bands.Intent.Counts.Records == 0 && bands.Intent.Counts.Decisions == 0;
        var product = bands.Product.Counts.Own == 0 && bands.Product.Neighbors.Count == 0 && bands.Product.Counts.Affects == 0;
        var code = bands.Code.Counts.Files == 0;
        return new LensSearched(anchors, basis.Map, depth, [.. _hops.Where(h => basis.Cascade.Hops.HasFlag(h.Hop)).Select(h => h.Name)], sources, new SearchedBands(Intent: new BandSearched(intent), Product: new BandSearched(product), Code: new BandSearched(code)));
    }
}
