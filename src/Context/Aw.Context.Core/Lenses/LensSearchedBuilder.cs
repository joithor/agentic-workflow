using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds <see cref="LensSearched"/> (scoping spec C6): what the answer rests on and which bands came back empty.</summary>
internal static class LensSearchedBuilder
{
    private static readonly (CascadeHops Hop, string Name)[] _hops =
        [(CascadeHops.State, "state"), (CascadeHops.Dependency, "dependency"), (CascadeHops.Flow, "flow"), (CascadeHops.Component, "component")];

    internal static LensSearched For(LensBasis basis, IReadOnlyList<string> anchors, int depth, LensBands bands)
    {
        var intent = bands.Intent.Counts.Records == 0 && bands.Intent.Counts.Decisions == 0;
        var product = bands.Product.Counts.Own == 0 && bands.Product.Neighbors.Count == 0 && bands.Product.Counts.Affects == 0;
        var code = bands.Code.Counts.Files == 0;
        var empty = new SearchedBands(Intent: new BandSearched(intent), Product: new BandSearched(product), Code: new BandSearched(code));
        var sources = new List<SearchedSource> { SearchedSources.Map(basis.MapCreated ?? basis.AsOf) };
        sources.AddRange(basis.IntentSources);
        sources.Add(SearchedSources.CodeIndex);
        return new LensSearched(anchors, basis.Map, depth, [.. _hops.Where(h => basis.Cascade.Hops.HasFlag(h.Hop)).Select(h => h.Name)], sources, empty);
    }

    /// <summary>The history answer's object (S-H): the map and the intent sources, then the history sources it read, no code-index row (not a history source), depth 0 and no hops.</summary>
    internal static LensSearched ForHistory(LensBasis basis, IReadOnlyList<string> anchors, IReadOnlyList<SearchedSource> intentSources, IReadOnlyList<SearchedSource> history, SearchedBands bands)
    {
        var sources = new List<SearchedSource> { SearchedSources.Map(basis.MapCreated ?? basis.AsOf) };
        sources.AddRange(intentSources);
        sources.AddRange(history);
        return new LensSearched(anchors, basis.Map, Depth: 0, [], sources, bands);
    }
}
