using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Features.Graph;

/// <summary>
/// Reads the map every graph answer starts from (spec §3, §6.1): the caller's gate (001, 020, 003), one snapshot of the
/// current version or the newest draft with its overrides (030 when there is none), the overrides applied, then a
/// session's repo scope (only the nodes its repo implements and what lies between them, else 003). Every walk runs on
/// this graph, so a session's cascade, neighbors and paths can't leave its scope.
/// </summary>
internal sealed class GraphReader(IGraphStore store)
{
    internal async ValueTask<Result<VisibleMap>> Read(ContextScope scope, bool draft, CancellationToken cancellationToken)
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

        var applied = OverrideApplier.Apply(map.Graph, map.Overrides).Graph;
        var visible = MapAccess.Visible(applied, [], scope.Caller);
        return visible.IsError ? visible.Error : new VisibleMap(map, visible.Value.Graph);
    }
}
