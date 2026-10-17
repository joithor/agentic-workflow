using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Reads the map every graph answer starts from (spec §3, §6.1), a domain service over <see cref="IGraphStore"/>: the
/// caller's gate (001, 020, 003), one snapshot of the current version or the newest draft with its overrides (030 when
/// there is none), the overrides applied, then a session's repo scope (only the nodes its repo implements, the records and decisions linked to them
/// (owner decision 2026-10-15, B) and what lies between them, else 003). Every walk runs on this graph, so a session's cascade, neighbors and paths can't leave its scope.
/// </summary>
/// <param name="store">The graph store.</param>
/// <param name="intents">The intent store whose synced records, decisions and links every read merges in.</param>
public sealed class MapReader(IGraphStore store, IIntentStore intents)
{
    /// <summary>The map <paramref name="scope"/>'s caller may read.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="draft">True for the newest draft instead of the current version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or SND-CONTEXT-001/003/004/020/030.</returns>
    public ValueTask<Result<VisibleMap>> Read(ContextScope scope, bool draft, CancellationToken cancellationToken) => ReadMap(scope, draft ? MapRead.NewestDraft : MapRead.Current, withIntent: true, cancellationToken);

    /// <summary>The map <paramref name="scope"/>'s caller may read as of <paramref name="read"/>, with a missing draft answering SND-CONTEXT-030 like <see cref="Read"/>.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="read">Which map.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or SND-CONTEXT-001/003/004/020/030/120.</returns>
    public ValueTask<Result<VisibleMap>> ReadAt(ContextScope scope, MapRead read, CancellationToken cancellationToken) => ReadMap(scope, read, withIntent: true, cancellationToken);

    /// <summary>The map as of <paramref name="read"/>: the current version, the newest draft or a retained version, with today's deny globs, scrub and session scope on it.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="read">Which map.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or SND-CONTEXT-001/003/004/020/030/044/120.</returns>
    public async ValueTask<Result<VisibleMap>> ReadViewAt(ContextScope scope, MapRead read, CancellationToken cancellationToken)
    {
        var map = await ReadMap(scope, read, withIntent: true, cancellationToken).ConfigureAwait(false);
        return read.Draft && map.IsError && map.Error.Code == ContextErrors.NoMap.Code ? NoDraft() : map;
    }

    /// <summary>The snapshot read asks for; a named version the store doesn't hold, or holds with another commit, is SND-CONTEXT-120.</summary>
    private async ValueTask<Result<MapSnapshot?>> Load(MapRead read, CancellationToken cancellationToken)
    {
        if (read.Version is not { } asked)
        {
            return await store.Snapshot(read.Draft, cancellationToken).ConfigureAwait(false);
        }

        var snapshot = await store.SnapshotAt(asked.Number, cancellationToken).ConfigureAwait(false);
        return snapshot.IsError || (snapshot.Value is { } held && asked.Names(held.Version)) ? snapshot : await NotRetained(asked, cancellationToken).ConfigureAwait(false);
    }

    private static Error NoDraft() =>
        ContextErrors.NoDraftMap.With("there is no draft map (fix: sindri context map build, sindri context map propose <file>, or name context-rebuild under services: in the profile, then retry; or drop --draft)");

    private async ValueTask<Result<VisibleMap>> ReadMap(ContextScope scope, MapRead read, bool withIntent, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(scope) is { } refused)
        {
            return refused;
        }

        var draft = read.Draft;
        var snapshot = await Load(read, cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError || snapshot.Value is not { } map)
        {
            return snapshot.IsError ? snapshot.Error
                : draft ? ContextErrors.NoMap.With("there is no draft map: drafts come from 'context map build', 'context map propose <file>' and the context-rebuild service (fix: ask without --draft)")
                : ContextErrors.NoMap.With("there is no map yet (fix: sindri context map import <file>)");
        }

        // A 'propose' row only adds its node to a draft: a pending proposal is never part of the current map (a promote makes an accepted one a node of the version).
        // A deny glob added after the map was built hides its paths now, before any rebuild: the one view every reader uses (review #220 run 3).
        var layer = withIntent ? await intents.Layer(cancellationToken).ConfigureAwait(false) : Kernel.Results.Success(IntentLayer.Empty);
        if (layer.IsError)
        {
            return layer.Error;
        }

        var visible = withIntent ? MapAccess.ViewWithIntent(layer.Value, map.Graph, map.Overrides, scope, pending: draft) : MapAccess.View(map.Graph, map.Overrides, scope, pending: draft);
        return visible.IsError ? visible.Error : new VisibleMap(map, visible.Value.Applied) { Conflicts = Seen(layer.Value.Conflicts, visible.Value.Applied) };
    }

    /// <summary>Reads the map as the structure alone: no synced records, decisions or links. The sync walks this, so a record never links to a record's own node.</summary>
    /// <param name="scope">The caller's scope.</param>
    /// <param name="draft">Whether to read the draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or the reason there is none.</returns>
    public ValueTask<Result<VisibleMap>> ReadWithoutIntent(ContextScope scope, bool draft, CancellationToken cancellationToken) =>
        ReadMap(scope, draft ? MapRead.NewestDraft : MapRead.Current, withIntent: false, cancellationToken);

    private static IReadOnlyList<DecisionConflict> Seen(IReadOnlyList<DecisionConflict> pairs, GraphSnapshot graph)
    {
        var present = graph.Decisions.Select(d => d.ToKey().Value).ToHashSet(StringComparer.Ordinal);
        return [.. pairs.Where(c => present.Contains(c.A.Value) && present.Contains(c.B.Value))];
    }

    /// <summary>The map the app's lenses and file-map reads start from: <see cref="Read"/>, but a draft that doesn't exist is SND-CONTEXT-044 (a missing map stays 030).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="draft">True for the newest draft instead of the current version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or SND-CONTEXT-001/003/004/020/030/044.</returns>
    public ValueTask<Result<VisibleMap>> ReadView(ContextScope scope, bool draft, CancellationToken cancellationToken) =>
        ReadViewAt(scope, draft ? MapRead.NewestDraft : MapRead.Current, cancellationToken);

    /// <summary>SND-CONTEXT-120 naming the current version and the retained range, so the fix is in the message.</summary>
    private async ValueTask<Result<MapSnapshot?>> NotRetained(MapVersionRef asked, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var current = versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Current);
        var retained = versions.Value.Where(v => v.Status == MapVersionStatus.Retained).Select(v => v.Number).Order().ToList();
        var parts = new List<string>();
        if (current is not null)
        {
            parts.Add($"current: {MapVersionRef.Tag(current.Number)}");
        }

        parts.Add(retained.Count == 0 ? "no earlier version is retained" : retained.Count == 1 ? $"retained: {MapVersionRef.Tag(retained[0])}" : $"retained: {MapVersionRef.Tag(retained[0])}..{MapVersionRef.Tag(retained[^1])}");
        return ContextErrors.MapVersionNotRetained.With($"map version {asked.Label} isn't held ({string.Join("; ", parts)}) (fix: read the current map, or name a version in that range)");
    }

    /// <summary>The current map and this call's text scrubbing, for the graph reads: the settings (001), the scrubbers built from them (001), then <see cref="Read"/>.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="scrubbers">Builds this call's scrubbers.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map and its text, or the first refusal.</returns>
    public async ValueTask<Result<(VisibleMap Map, MapText Text)>> Open(ContextScope scope, IScrubberFactory scrubbers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(scrubbers);
        var built = SettingsReader.Read(scope.Settings).Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var map = await Read(scope, draft: false, cancellationToken).ConfigureAwait(false);
        return map.IsError ? map.Error : (map.Value, new MapText(built.Value, FencePolicies.For(scope.Caller)));
    }
}
