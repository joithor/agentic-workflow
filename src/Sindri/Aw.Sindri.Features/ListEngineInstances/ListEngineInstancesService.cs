using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.ListEngineInstances;

/// <summary>
/// 'GET /engine/instances?state=&amp;stuck=' (API spec §3.8, E-30): the running instances by address, each with what it waits on and whether it is stuck against the clock
/// (<see cref="EngineStuck"/>). The filters narrow the list, not 'pending', which counts every running instance. A 'stuck' that isn't 'true' or 'false' is SND-API-013.
/// </summary>
/// <param name="engine">The Temporal engine's read side.</param>
/// <param name="clock">The clock the stuck rule reads.</param>
internal sealed class ListEngineInstancesService(IEngineReader engine, TimeProvider clock) : IService<ListEngineInstancesRequest, EngineInstanceList>
{
    public async ValueTask<Result<EngineInstanceList>> Handle(ListEngineInstancesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool? stuck = null;
        if (request.Stuck is { } text)
        {
            if (!TryStuck(text, out var wanted))
            {
                return SindriErrors.InvalidQuery.With($"stuck is 'true' or 'false', not '{text}'");
            }

            stuck = wanted;
        }

        var running = await engine.Instances(cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        var now = clock.GetUtcNow();
        var instances = running.Value
            .Where(i => request.State is null || string.Equals(i.State, request.State, StringComparison.Ordinal))
            .OrderBy(static i => i.Address.Value, StringComparer.Ordinal)
            .Select(i => EngineProjection.Instance(i, now))
            .Where(i => stuck is null || i.Stuck == stuck)
            .ToList();
        return new EngineInstanceList(instances, EngineProjection.Pending(running.Value, now), now);
    }

    private static bool TryStuck(string text, out bool stuck)
    {
        stuck = string.Equals(text, "true", StringComparison.Ordinal);
        return stuck || string.Equals(text, "false", StringComparison.Ordinal);
    }
}
