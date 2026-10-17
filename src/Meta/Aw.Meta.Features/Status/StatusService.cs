using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Status;

/// <summary>Where the meta stands: what it owes, the merge queue, the owner's queue and the lane map.</summary>
/// <param name="config">The team config.</param>
/// <param name="values">Holds the last health summary.</param>
/// <param name="status">Reads the parts.</param>
/// <param name="resources">Reads memory and disk for the builders advice.</param>
/// <param name="advisor">Recomputes the heavy reservation and computes the builders advice.</param>
/// <param name="clock">The current time.</param>
internal sealed class StatusService(IMetaConfig config, StatusReader status, IKeyValueStore values, IHostResources resources, ResourceAdvisor advisor, TimeProvider clock) : IService<StatusRequest, StatusResponse>
{
    public async ValueTask<Result<StatusResponse>> Handle(StatusRequest request, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var read = await status.Read(team.Value.MetaLane, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var health = await values.Get(KvKeys.HealthLast, cancellationToken).ConfigureAwait(false);
        if (read.IsError || health.IsError)
        {
            return read.IsError ? read.Error : health.Error;
        }

        // The reservation and the advice are extras: a host or file that can't be read leaves them out instead of failing the status.
        var host = await resources.Read(cancellationToken).ConfigureAwait(false);
        var advice = host.IsError ? null : await advisor.Advise(team.Value, host.Value, refresh: true, cancellationToken).ConfigureAwait(false);
        return read.Value with { LastHealth = health.Value, Heavy = advice?.IsSuccess == true ? advice.Value.Heavy : null, Builders = advice?.IsSuccess == true ? advice.Value.Builders : null };
    }
}
