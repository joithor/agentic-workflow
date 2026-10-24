using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApproveRecipe;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow/recipes/{id}@{hash}/approve (engine spec §6.3; protected): the body is a human's challenge and its typed text, or an agent's eval files.</summary>
internal static class ApproveRecipeEndpoint
{
    internal static RouteGroupBuilder MapApproveRecipe(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/recipes/{id}@{hash}/approve", static async (string id, string hash, ApproveRecipeBody body,
                IService<ApproveRecipeRequest, ApproveRecipeResponse> service, CancellationToken ct)
            => (await service.Handle(new ApproveRecipeRequest(RecipeId.From(id), RecipeRoute.Hash(hash), body.Answer(), body.Evals), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
