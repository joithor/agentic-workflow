using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.ListEngineInstances;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/engine/instances[?state=&amp;stuck=true|false] (API spec §3.8, E-30; query): the running instances as Temporal sees them. A 'stuck' outside its set is SND-API-013; SND-ENGINE-001 (503) when the server doesn't answer.</summary>
internal static class ListEngineInstancesEndpoint
{
    internal static RouteGroupBuilder MapListEngineInstances(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/engine/instances", static async (string? state, string? stuck, IService<ListEngineInstancesRequest, EngineInstanceList> service, CancellationToken ct)
            => (await service.Handle(new ListEngineInstancesRequest(state, stuck), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
