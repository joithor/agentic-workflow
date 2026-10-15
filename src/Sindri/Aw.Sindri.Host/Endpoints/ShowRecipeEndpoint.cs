using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ShowRecipe;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/workflow/recipes/{id}@{hash} (engine spec §6.3; query): the recipe's line and its approval preview, the hash in full or by a prefix of 8 or more digits.</summary>
internal static class ShowRecipeEndpoint
{
    internal static RouteGroupBuilder MapShowRecipe(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/recipes/{id}@{hash}", static async (string id, string hash, IService<ShowRecipeRequest, ShowRecipeResponse> service, CancellationToken ct)
            => (await service.Handle(new ShowRecipeRequest(RecipeId.From(id), hash), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
