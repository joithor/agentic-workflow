using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Lanes;

/// <summary>
/// Maps each configured lane to the live session working it (a session named "team-lane", else one that announced the lane)
/// and saves the map when it changed. A changed map opens a broadcast-lanes obligation (it is derived: not every lane has
/// been sent the new version).
/// </summary>
/// <param name="config">The team config.</param>
/// <param name="refresher">Maps and saves.</param>
/// <param name="clock">Dates a changed map.</param>
internal sealed class RefreshLanesService(IMetaConfig config, LaneRefresher refresher, TimeProvider clock) : IService<RefreshLanesRequest, LanesResponse>
{
    public async ValueTask<Result<LanesResponse>> Handle(RefreshLanesRequest request, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var refreshed = await refresher.Run(team.Value, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return refreshed.IsError ? refreshed.Error : LanesReport.Of(team.Value, refreshed.Value);
    }
}
