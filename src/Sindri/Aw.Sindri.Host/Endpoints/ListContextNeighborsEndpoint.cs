using Aw.Context.Core.Graph;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/neighbors?key=&amp;depth= (context spec §8.5).</summary>
internal static class ListContextNeighborsEndpoint
{
    internal static RouteGroupBuilder MapListContextNeighbors(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/neighbors", static async (IService<ListContextNeighborsRequest, NeighborsView> service, string? key, int? depth, CancellationToken ct)
            => (await service.Handle(new ListContextNeighborsRequest(key ?? string.Empty, depth), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
