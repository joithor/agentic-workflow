using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.GetEngine;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/engine (API spec §3.8, E-30; query): the Temporal server, the worker, the mirror's lag and what the instances wait on. SND-ENGINE-001 (503) when the server doesn't answer.</summary>
internal static class GetEngineEndpoint
{
    internal static RouteGroupBuilder MapGetEngine(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/engine", static async (IService<GetEngineRequest, EngineView> service, CancellationToken ct)
            => (await service.Handle(new GetEngineRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
