using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Features.StopCheck;

/// <summary>
/// The Stop hook's question. Any session but the registered meta may stop. For the meta: sync the transcript, refresh the lane
/// map, evaluate what is owed, and block while anything is open, except that the identical open set blocks at most
/// MaxBlocks times in a row (a continuation is then let through with a warning and an incident in the log).
/// </summary>
/// <param name="session">Tells the meta session from the rest.</param>
/// <param name="sweep">Syncs, refreshes and evaluates.</param>
/// <param name="guard">The loop guard.</param>
/// <param name="clock">Dates the incident.</param>
internal sealed class StopCheckService(MetaSession session, MetaSweep sweep, StopGuard guard, TimeProvider clock) : IService<StopCheckRequest, StopCheckResponse>
{
    public async ValueTask<Result<StopCheckResponse>> Handle(StopCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await session.Resolve(request.Session, cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        if (team.Value is not { } config)
        {
            return new StopCheckResponse(IsMeta: false, Blocked: false, [], Warning: null);
        }

        var now = clock.GetUtcNow();
        var open = await sweep.Run(request.Transcript, config, now, cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var outcome = await guard.Decide(open.Value, request.HookActive, request.MaxBlocks, now, cancellationToken).ConfigureAwait(false);
        return outcome.IsError ? outcome.Error : new StopCheckResponse(IsMeta: true, outcome.Value.Block, open.Value, outcome.Value.Warning);
    }
}
