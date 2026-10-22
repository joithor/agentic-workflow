using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.GetEngineHistory;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/engine/instances/{id}/history[?page=&amp;address=] (API spec §3.8, E-30; query): one page of an instance's Temporal history, newest first; the instance is the
/// 'WFI-' id in the path, or the address in '?address=' (which wins). A page below 1 is SND-API-013, an instance with no execution SND-ENGINE-404 (404) and a server that
/// doesn't answer SND-ENGINE-001 (503).
/// </summary>
internal static class GetEngineHistoryEndpoint
{
    internal static RouteGroupBuilder MapGetEngineHistory(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/engine/instances/{id}/history", static async (string id, string? page, string? address, IService<GetEngineHistoryRequest, EngineHistory> service, CancellationToken ct)
            => (await service.Handle(new GetEngineHistoryRequest(id, address, page), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
