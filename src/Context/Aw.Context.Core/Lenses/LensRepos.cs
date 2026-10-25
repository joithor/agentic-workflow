using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Writes each band item's repo (<see cref="NodeRepos"/>) once the bands are built.</summary>
internal static class LensRepos
{
    /// <summary>The bands with every item's repo set from <paramref name="graph"/>.</summary>
    /// <param name="bands">The built bands.</param>
    /// <param name="graph">The graph the bands were built over.</param>
    /// <param name="mapRepo">The repo the map was built for, or null.</param>
    /// <returns>The bands, items copied with their repo.</returns>
    internal static LensBands Stamp(LensBands bands, GraphSnapshot graph, string? mapRepo) => Stamp(bands, NodeRepos.Index(graph, mapRepo));

    internal static LensBands Stamp(LensBands bands, IReadOnlyDictionary<string, string?> repos)
    {
        var product = bands.Product with
        {
            Own = [.. bands.Product.Own.Select(i => i with { Repo = repos.GetValueOrDefault(i.Node.Value) })],
            Neighbors = [.. bands.Product.Neighbors.Select(r => r with { Repo = repos.GetValueOrDefault(r.Node.Value) })],
            Affects = [.. bands.Product.Affects.Select(r => r with { Repo = repos.GetValueOrDefault(r.Node.Value) })],
        };
        var intent = bands.Intent with
        {
            Records = [.. bands.Intent.Records.Select(r => r with { Repo = repos.GetValueOrDefault(r.Key.Value) })],
            Decisions = [.. bands.Intent.Decisions.Select(d => d with { Repo = repos.GetValueOrDefault(d.Node.Value) })],
        };
        var code = bands.Code with { Files = [.. bands.Code.Files.Select(f => f with { Repo = repos.GetValueOrDefault("code:" + f.Path) })] };
        return new LensBands(intent, product, code);
    }
}
