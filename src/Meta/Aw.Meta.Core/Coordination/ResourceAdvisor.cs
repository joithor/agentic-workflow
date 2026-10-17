using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>What the host's room means for the team: the kept heavy reservation, and the builders each lane should run.</summary>
/// <param name="reserve">The team's kept reservation.</param>
/// <param name="reservation">Recomputes the reservation when asked.</param>
/// <param name="clock">Tells whether the kept reservation is stale.</param>
public sealed class ResourceAdvisor(IHeavyReserve reserve, HeavyReservation reservation, TimeProvider clock)
{
    /// <summary>Advises from a reading.</summary>
    /// <param name="team">The team config; null (no team config) means the defaults and one lane, and no reservation.</param>
    /// <param name="host">The host's figures.</param>
    /// <param name="refresh">Whether to recompute the reservation from the queue first (otherwise the kept one is shown, with its staleness).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reservation (null when none is kept or the team turned it off) and the builders advice, or the file's error.</returns>
    public async ValueTask<Result<ResourceAdvice>> Advise(TeamConfig? team, HostResources host, bool refresh, CancellationToken cancellationToken)
    {
        var heavy = await Heavy(team, refresh, cancellationToken).ConfigureAwait(false);
        if (heavy.IsError)
        {
            return heavy.Error;
        }

        var lanes = team is null ? 1 : team.Lanes.Count(lane => lane != team.MetaLane);
        var advice = BuilderAdvice.Compute(host, team?.Resources ?? new ResourcesConfig(), team?.Builders ?? new BuildersConfig(), lanes);
        return new ResourceAdvice(heavy.Value, advice);
    }

    private async ValueTask<Result<HeavySlots?>> Heavy(TeamConfig? team, bool refresh, CancellationToken cancellationToken)
    {
        if (team is null || !team.Heavy.EffectiveReserve.Enabled)
        {
            return Results.Success<HeavySlots?>(value: null);
        }

        if (refresh)
        {
            return await reservation.Apply(team, cancellationToken).ConfigureAwait(false);
        }

        var kept = await reserve.Read(cancellationToken).ConfigureAwait(false);
        return kept.IsError
            ? kept.Error
            : Results.Success<HeavySlots?>(kept.Value is { } record
                ? new HeavySlots(team.Heavy.Cap, record.Reserve, record.Depth, record.At, clock.GetUtcNow() - record.At >= HeavyReservation.StaleAfter)
                : null);
    }
}
