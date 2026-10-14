using Aw.Kernel.Services;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.RevokeRecipe;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow/recipes/{id}@{hash}/revoke (engine spec §6.3; protected): the body is the challenge and its typed text.</summary>
internal static class RevokeRecipeEndpoint
{
    internal static RouteGroupBuilder MapRevokeRecipe(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/recipes/{id}@{hash}/revoke", static async (string id, string hash, ChallengeAnswer answer,
                IService<RevokeRecipeRequest, RevokeRecipeResponse> service, CancellationToken ct)
            => (await service.Handle(new RevokeRecipeRequest(RecipeId.From(id), RecipeRoute.Hash(hash), answer), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
