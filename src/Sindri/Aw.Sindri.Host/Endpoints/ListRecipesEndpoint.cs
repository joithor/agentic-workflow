using Aw.Kernel.Services;
using Aw.Sindri.Features.ListRecipes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/workflow/recipes[?stale=true] (engine spec §6.3; query): every registered recipe with its inputs (README X34), or the stale ones.</summary>
internal static class ListRecipesEndpoint
{
    internal static RouteGroupBuilder MapListRecipes(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/recipes", static async (bool? stale, IService<ListRecipesRequest, ListRecipesResponse> service, CancellationToken ct)
            => (await service.Handle(new ListRecipesRequest(stale ?? false), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
