using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.GetApproveAllPlan;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/workflow/recipes/approve-all (setup-once spec §5.3, §7.2; protected): the body is the challenge and its typed text, as ApproveRecipe's. It answers 202
/// JobStarted for the job that approves the plan. When nothing waits it answers 200 with an empty result and starts no job (a re-run changes nothing); that answer is
/// still the protected action's, so a caller the authorization table refuses is refused here too.
/// </summary>
internal static class ApproveAllRecipesEndpoint
{
    internal static RouteGroupBuilder MapApproveAllRecipes(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/recipes/approve-all", Handle).WithMetadata(new JobRouteMetadata(typeof(ApproveAllRecipesRequest)));
        return group;
    }

    private static async Task<IResult> Handle(ChallengeAnswer? answer, HttpContext http)
    {
        var services = http.RequestServices;
        var ct = http.RequestAborted;
        var plan = await services.GetRequiredService<IService<GetApproveAllPlanRequest, ApproveAllPlan>>().Handle(new GetApproveAllPlanRequest(), ct).ConfigureAwait(false);
        if (plan is { IsSuccess: true, Value: { Recipes.Count: 0, OwnApproval.Count: 0 } })
        {
            var row = services.GetRequiredService<ICommandCatalog>().For(typeof(ApproveAllRecipesRequest))[0];
            return Authorization.Decide(row.Class, services.GetRequiredService<ICallerContextAccessor>().Current, row).Refusal is { } refused
                ? HttpResults.Problem(refused)
                : Results.Ok(ApproveAllResult.Nothing);
        }

        return answer is null
            ? HttpResults.Problem(SindriErrors.InvalidInput.With("the request body is empty (send the challenge and its typed text)"))
            : (await services.GetRequiredService<IService<ApproveAllRecipesRequest, JobStartedResponse>>().Handle(new ApproveAllRecipesRequest(answer), ct).ConfigureAwait(false)).ToHttpResult();
    }
}
