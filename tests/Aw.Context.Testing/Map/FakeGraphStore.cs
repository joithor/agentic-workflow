using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Testing.Map;

/// <summary>The in-memory graph store; <see cref="GraphStoreContract"/> holds it to the SQLite store's behavior.</summary>
public sealed class FakeGraphStore : IGraphStore
{
    private readonly List<(MapVersion Version, GraphSnapshot Graph)> _versions = [];
    private readonly List<MapOverride> _overrides = [];

    /// <summary>When set, every call fails with it (a store that can't open).</summary>
    public Error? Failure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion?>> Current(CancellationToken cancellationToken) =>
        Done(() => Results.Success(_versions.Select(v => v.Version).FirstOrDefault(v => v.Status == MapVersionStatus.Current)));

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<MapVersion>>> Versions(CancellationToken cancellationToken) =>
        Done(() => Results.Success<IReadOnlyList<MapVersion>>([.. _versions.Select(v => v.Version).OrderByDescending(v => v.Number)]));

    /// <inheritdoc/>
    public ValueTask<Result<GraphSnapshot>> Read(int version, CancellationToken cancellationToken) =>
        Done(() => Results.Success(_versions.Find(v => v.Version.Number == version).Graph ?? GraphSnapshot.Empty));

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<MapOverride>>> Overrides(CancellationToken cancellationToken) =>
        Done(() => Results.Success<IReadOnlyList<MapOverride>>([.. _overrides.OrderBy(o => o.At).ThenBy(o => o.Id.Value, StringComparer.Ordinal)]));

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> SaveCurrent(MapImport map, DateTimeOffset created, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(map);
        for (var i = 0; i < _versions.Count; i++)
        {
            if (_versions[i].Version.Status == MapVersionStatus.Current)
            {
                _versions[i] = _versions[i] with { Version = _versions[i].Version with { Status = MapVersionStatus.Retained } };
            }
        }

        var version = new MapVersion(_versions.Count == 0 ? 1 : _versions.Max(v => v.Version.Number) + 1, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Current);
        _versions.Add((version, map.Graph));
        _overrides.AddRange(map.Overrides.Where(o => !_overrides.Exists(e => e.Id == o.Id)));
        return Results.Success(version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken) =>
        Done(() => Results.Success(_versions.RemoveAll(v => versions.Contains(v.Version.Number) && v.Version.Status == MapVersionStatus.Retained)));

    private ValueTask<Result<T>> Done<T>(Func<Result<T>> work) => ValueTask.FromResult(Failure is { } failure ? failure : work());
}
