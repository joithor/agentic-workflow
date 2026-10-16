using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ShowRecipe;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/recipes/{id} and /{id}@{hash} (API spec §3.8; query): the recipe as <see cref="RecipeView"/>. The bare id is the
/// approved version; with a hash (in full or a prefix of 8 or more digits) it is that registered version, approved or not.
/// </summary>
internal static class ShowRecipeEndpoint
{
    internal static RouteGroupBuilder MapShowRecipe(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/recipes/{id}@{hash}", static async (string id, string hash, IService<ShowRecipeRequest, RecipeView> service, CancellationToken ct)
            => (await service.Handle(new ShowRecipeRequest(RecipeId.From(id), hash), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/workflow/recipes/{id}", static async (string id, IService<ShowRecipeRequest, RecipeView> service, CancellationToken ct)
            => (await service.Handle(new ShowRecipeRequest(RecipeId.From(id)), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
