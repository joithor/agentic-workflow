using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Lenses;

/// <summary>What a lenses view was read from.</summary>
/// <param name="Map">The map read ('v13').</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="AsOf">When the view is built.</param>
public sealed record LensBasis(string Map, bool Draft, DateTimeOffset AsOf)
{
    /// <summary>The commit the map was built at (its first seven characters), or null for an imported map that names none; answers carry it beside 'mapVersion', which stays 'v&lt;N&gt;'.</summary>
    public string? MapCommit { get; init; }

    /// <summary>The repo the map version was built for, or null when the caller has none; the repo of a node that names none (<see cref="Aw.Context.Core.Graph.NodeRepos"/>).</summary>
    public string? MapRepo { get; init; }

    /// <summary>What the cascade may do for this call: the hops asked for and the component fan-out cap.</summary>
    public CascadeOptions Cascade { get; init; } = CascadeOptions.Default;

    /// <summary>When the map version was written (the answer's map source and map coverage read it); null when the caller has none, which reads as <see cref="AsOf"/>.</summary>
    public DateTimeOffset? MapCreated { get; init; }

    /// <summary>The repo's tracked files, for map coverage; empty counts nothing.</summary>
    public IReadOnlyList<string> Files { get; init; } = [];

    /// <summary>The intent sources' rows (the builder adds the map and the code index).</summary>
    public IReadOnlyList<SearchedSource> IntentSources { get; init; } = [];
}
