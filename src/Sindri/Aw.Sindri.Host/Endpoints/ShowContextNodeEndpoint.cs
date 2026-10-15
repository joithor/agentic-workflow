using Aw.Context.Core.Graph;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/node?key= (context spec §8.5): the key travels in the query string (keys hold '/' and '#'); none answers SND-CONTEXT-003.</summary>
internal static class ShowContextNodeEndpoint
{
    internal static RouteGroupBuilder MapShowContextNode(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/node", static async (IService<ShowContextNodeRequest, NodeView> service, string? key, CancellationToken ct)
            => (await service.Handle(new ShowContextNodeRequest(key ?? string.Empty), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
