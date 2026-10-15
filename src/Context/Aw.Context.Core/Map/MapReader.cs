using Aw.Context.Core.Engine;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Reads the map every graph answer starts from (spec §3, §6.1), a domain service over <see cref="IGraphStore"/>: the
/// caller's gate (001, 020, 003), one snapshot of the current version or the newest draft with its overrides (030 when
/// there is none), the overrides applied, then a session's repo scope (only the nodes its repo implements and what lies
/// between them, else 003). Every walk runs on this graph, so a session's cascade, neighbors and paths can't leave its scope.
/// </summary>
/// <param name="store">The graph store.</param>
public sealed class MapReader(IGraphStore store)
{
    /// <summary>The map <paramref name="scope"/>'s caller may read.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="draft">True for the newest draft instead of the current version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or SND-CONTEXT-001/003/004/020/030.</returns>
    public async ValueTask<Result<VisibleMap>> Read(ContextScope scope, bool draft, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(scope) is { } refused)
        {
            return refused;
        }

        var snapshot = await store.Snapshot(draft, cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError || snapshot.Value is not { } map)
        {
            return snapshot.IsError ? snapshot.Error
                : draft ? ContextErrors.NoMap.With("there is no draft map: drafts come from 'context map build' (S-4) (fix: ask without --draft)")
                : ContextErrors.NoMap.With("there is no map yet (fix: sindri context map import <file>)");
        }

        // A 'propose' row only adds its node to a draft: a pending proposal is never part of the current map (a promote makes an accepted one a node of the version).
        var layer = draft ? map.Overrides : [.. map.Overrides.Where(o => o.Action != OverrideAction.Propose)];
        var applied = OverrideApplier.Apply(map.Graph, layer).Graph;
        var visible = MapAccess.Visible(applied, [], scope.Caller);
        return visible.IsError ? visible.Error : new VisibleMap(map, visible.Value.Graph);
    }

    /// <summary>The map the app's lenses and file-map reads start from: <see cref="Read"/>, but a draft that doesn't exist is SND-CONTEXT-044 (a missing map stays 030).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="draft">True for the newest draft instead of the current version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The visible map, or SND-CONTEXT-001/003/004/020/030/044.</returns>
    public async ValueTask<Result<VisibleMap>> ReadView(ContextScope scope, bool draft, CancellationToken cancellationToken)
    {
        var map = await Read(scope, draft, cancellationToken).ConfigureAwait(false);
        return draft && map.IsError && map.Error.Code == ContextErrors.NoMap.Code
            ? ContextErrors.NoDraftMap.With("there is no draft map (fix: sindri context map build, then retry; or drop --draft)")
            : map;
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
