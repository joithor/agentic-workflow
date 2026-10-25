using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetEngine;

/// <summary>'GET /engine' (API spec §3.8, E-30): the server, worker and mirror as the engine reader reports them, and what the running instances wait on, stamped with the clock. A server that doesn't answer is SND-ENGINE-001.</summary>
/// <param name="engine">The Temporal engine's read side.</param>
/// <param name="clock">The clock the stuck rule and 'asOf' read.</param>
internal sealed class GetEngineService(IEngineReader engine, TimeProvider clock) : IService<GetEngineRequest, EngineView>
{
    public async ValueTask<Result<EngineView>> Handle(GetEngineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var server = await engine.Server(cancellationToken).ConfigureAwait(false);
        if (server.IsError)
        {
            return server.Error;
        }

        var worker = await engine.Worker(cancellationToken).ConfigureAwait(false);
        if (worker.IsError)
        {
            return worker.Error;
        }

        var mirror = await engine.Mirror(cancellationToken).ConfigureAwait(false);
        if (mirror.IsError)
        {
            return mirror.Error;
        }

        var instances = await engine.Instances(cancellationToken).ConfigureAwait(false);
        if (instances.IsError)
        {
            return instances.Error;
        }

        var now = clock.GetUtcNow();
        var running = instances.Value.Count(static i => i.Waiting is { } w && string.Equals(w.Kind, EngineWaitKinds.Activity, StringComparison.Ordinal));   // the task queue doesn't say how many activities run, the instances do
        return new EngineView(server.Value, worker.Value with { ActivitiesRunning = running }, mirror.Value, EngineProjection.Pending(instances.Value, now), now);
    }
}
