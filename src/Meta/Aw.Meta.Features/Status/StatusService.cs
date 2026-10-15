using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Status;

/// <summary>Where the meta stands: what it owes, the merge queue, the owner's queue and the lane map.</summary>
/// <param name="config">The team config.</param>
/// <param name="values">Holds the last health summary and the pull requests the watch saw end.</param>
/// <param name="status">Reads the parts.</param>
/// <param name="clock">The current time.</param>
internal sealed class StatusService(IMetaConfig config, StatusReader status, IKeyValueStore values, TimeProvider clock) : IService<StatusRequest, StatusResponse>
{
    public async ValueTask<Result<StatusResponse>> Handle(StatusRequest request, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var ended = await values.Get(KvKeys.EndedPrs, cancellationToken).ConfigureAwait(false);
        if (ended.IsError)
        {
            return ended.Error;
        }

        var read = await status.Read(team.Value.MetaLane, clock.GetUtcNow(), EndedPrs.Parse(ended.Value), cancellationToken).ConfigureAwait(false);
        var health = await values.Get(KvKeys.HealthLast, cancellationToken).ConfigureAwait(false);
        return read.IsError || health.IsError ? read.IsError ? read.Error : health.Error : read.Value with { LastHealth = health.Value };
    }
}
