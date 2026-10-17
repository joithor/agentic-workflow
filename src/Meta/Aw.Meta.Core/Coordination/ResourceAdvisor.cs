using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>What the host's room means for the team: the heavy slots and reservation, and the builders each lane should run.</summary>
/// <param name="reserve">The cap and reservation files.</param>
public sealed class ResourceAdvisor(IHeavyReserve reserve)
{
    /// <summary>Advises from a reading.</summary>
    /// <param name="team">The team config; null (no team config) means the defaults and one lane.</param>
    /// <param name="host">The host's figures.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The heavy slots and the builders advice, or the file's error.</returns>
    public async ValueTask<Result<ResourceAdvice>> Advise(TeamConfig? team, HostResources host, CancellationToken cancellationToken)
    {
        var heavy = await reserve.Read(cancellationToken).ConfigureAwait(false);
        if (heavy.IsError)
        {
            return heavy.Error;
        }

        var lanes = team is null ? 1 : team.Lanes.Count(lane => lane != team.MetaLane);
        var advice = BuilderAdvice.Compute(host, team?.Resources ?? new ResourcesConfig(), team?.Builders ?? new BuildersConfig(), lanes);
        return new ResourceAdvice(heavy.Value, advice);
    }
}
