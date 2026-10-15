using Aw.Context.Core;
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
    private int _last;   // version numbers are never reused, as with the SQLite store's AUTOINCREMENT

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
    public ValueTask<Result<MapSnapshot?>> Snapshot(bool draft, CancellationToken cancellationToken) => Done(() =>
    {
        var wanted = draft ? MapVersionStatus.Draft : MapVersionStatus.Current;
        var found = _versions.Where(v => v.Version.Status == wanted).OrderByDescending(v => v.Version.Number).Select(v => ((MapVersion, GraphSnapshot)?)v).FirstOrDefault();
        IReadOnlyList<MapOverride> overrides = [.. _overrides.OrderBy(o => o.At).ThenBy(o => o.ToId().Value, StringComparer.Ordinal)];
        return Results.Success(found is { } f ? new MapSnapshot(f.Item1, f.Item2, overrides) : null);
    });

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<MapOverride>>> Overrides(CancellationToken cancellationToken) =>
        Done(() => Results.Success<IReadOnlyList<MapOverride>>([.. _overrides.OrderBy(o => o.At).ThenBy(o => o.ToId().Value, StringComparer.Ordinal)]));

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

        var version = new MapVersion(++_last, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Current);
        _versions.Add((version, map.Graph));
        _ = Append(map.Overrides);
        return Results.Success(version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken) =>
        Done(() => Results.Success(_versions.RemoveAll(v => versions.Contains(v.Version.Number) && v.Version.Status == MapVersionStatus.Retained)));

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> SaveDraft(MapImport map, DateTimeOffset created, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(map);
        _ = _versions.RemoveAll(v => v.Version.Status == MapVersionStatus.Draft);
        var version = new MapVersion(++_last, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Draft);
        _versions.Add((version, map.Graph));
        _ = Append(map.Overrides);
        return Results.Success(version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> OpenDraft(DateTimeOffset created, CancellationToken cancellationToken) => Done(() =>
    {
        if (_versions.Select(v => v.Version).FirstOrDefault(v => v.Status == MapVersionStatus.Draft) is { } open)
        {
            return Results.Success(open);
        }

        if (_versions.FirstOrDefault(v => v.Version.Status == MapVersionStatus.Current) is not { Version: { } current } source)
        {
            return Results.Failure<MapVersion>(ContextErrors.NoMap.With("there is no current map to open a draft from"));
        }

        var copy = current with { Number = ++_last, Created = created, Status = MapVersionStatus.Draft };
        _versions.Add((copy, source.Graph));
        return Results.Success(copy);
    });

    /// <inheritdoc/>
    public ValueTask<Result<MapVersion>> Promote(int draft, CancellationToken cancellationToken) => Done(() =>
    {
        var index = _versions.FindIndex(v => v.Version.Number == draft && v.Version.Status == MapVersionStatus.Draft);
        if (index < 0)
        {
            return Results.Failure<MapVersion>(ContextErrors.PromoteRefused.With("that version isn't a draft"));
        }

        for (var i = 0; i < _versions.Count; i++)
        {
            if (_versions[i].Version.Status == MapVersionStatus.Current)
            {
                _versions[i] = _versions[i] with { Version = _versions[i].Version with { Status = MapVersionStatus.Retained } };
            }
        }

        _versions[index] = _versions[index] with { Version = _versions[index].Version with { Status = MapVersionStatus.Current } };
        return Results.Success(_versions[index].Version);
    });

    /// <inheritdoc/>
    public ValueTask<Result<int>> AppendOverrides(IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken) => Done(() =>
    {
        ArgumentNullException.ThrowIfNull(overrides);
        return Results.Success(Append(overrides));
    });

    private int Append(IReadOnlyList<MapOverride> overrides)
    {
        var added = 0;
        foreach (var o in overrides.Where(o => !_overrides.Exists(e => e.ToId() == o.ToId())))
        {
            _overrides.Add(o);
            added++;
        }

        return added;
    }

    private ValueTask<Result<T>> Done<T>(Func<Result<T>> work) => ValueTask.FromResult(Failure is { } failure ? failure : work());
}
