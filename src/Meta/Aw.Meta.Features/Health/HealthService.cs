using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Checks the box: memory by kind of process, orphaned Temporal dev servers (killed by pid with the flag), duplicate MCP
/// servers, lanes with no live session and who holds the heavy-job lock. The result is kept for status.
/// </summary>
/// <param name="probe">Reads the host.</param>
/// <param name="lanes">Finds missing lanes.</param>
/// <param name="killer">Terminates orphans.</param>
/// <param name="values">Keeps the summary.</param>
/// <param name="clock">Dates the summary.</param>
internal sealed class HealthService(HostProbe probe, LaneCheck lanes, OrphanKiller killer, IKeyValueStore values, TimeProvider clock)
    : IService<HealthRequest, HealthResponse>
{
    public async ValueTask<Result<HealthResponse>> Handle(HealthRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var host = await probe.Read(cancellationToken).ConfigureAwait(false);
        var missing = await lanes.Missing(cancellationToken).ConfigureAwait(false);
        if (host.IsError || missing.IsError)
        {
            return host.IsError ? host.Error : missing.Error;
        }

        var processes = host.Value.Snapshot.Processes;
        var orphans = HostRules.Orphans(processes);
        var killed = request.KillOrphans ? await killer.Kill(orphans, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<int>>([]);
        if (killed.IsError)
        {
            return killed.Error;
        }

        var low = HostRules.MemoryLow(host.Value.Resources);
        var remaining = orphans.Count(o => !killed.Value.Contains(o.Pid));
        var attention = remaining > 0 || low || missing.Value is { Count: > 0 };
        var response = new HealthResponse(
            ProcessGroups.Group(processes), processes.Count, host.Value.Resources, orphans, killed.Value, HostRules.Duplicates(processes), missing.Value, host.Value.Locks, low, attention);
        var kept = await values.Set(KvKeys.HealthLast, Summary(response, remaining), cancellationToken).ConfigureAwait(false);
        return kept.IsError ? kept.Error : response;
    }

    private string Summary(HealthResponse health, int orphans) => string.Create(
        CultureInfo.InvariantCulture,
        $"{clock.GetUtcNow():O} {(health.Attention ? "attention" : "ok")}: orphans={orphans} missing-lanes={(health.MissingLanes is null ? "-" : health.MissingLanes.Count)} memory-available={health.Host.AvailablePercent}%");
}
