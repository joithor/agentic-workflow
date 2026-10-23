using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.ApproveAllRecipes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/workflow/recipes/approve-all (setup-once spec §5.3, §7.2; protected): no body is read. It answers JobStarted (200, like every job route) for the job that approves the plan. When nothing waits it answers 200 with an empty result and starts no job (a re-run changes nothing); that answer is
/// still the protected action's, so a caller the authorization table refuses is refused here too.
/// </summary>
internal static class ApproveAllRecipesEndpoint
{
    internal static RouteGroupBuilder MapApproveAllRecipes(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow/recipes/approve-all", static async Task<IResult> (HttpContext http) =>
            {
                var services = http.RequestServices;
                var ct = http.RequestAborted;
                var plan = await services.GetRequiredService<ApproveAllPlanReader>().Read(ct).ConfigureAwait(false);
                if (plan is { IsSuccess: true, Value.NothingToHold: true })
                {
                    var row = services.GetRequiredService<ICommandCatalog>().For(typeof(ApproveAllRecipesRequest))[0];
                    Result<ApproveAllResult> empty = Authorization.Decide(row.Class, services.GetRequiredService<ICallerContextAccessor>().Current, row).Refusal is { } refused
                        ? refused
                        : ApproveAllResult.Nothing;
                    return empty.ToHttpResult();
                }

                return (await services.GetRequiredService<IService<ApproveAllRecipesRequest, JobStartedResponse>>().Handle(new ApproveAllRecipesRequest(), ct).ConfigureAwait(false)).ToHttpResult();
            }).WithMetadata(new JobRouteMetadata(typeof(ApproveAllRecipesRequest)));   // why: a lambda like every endpoint: a method here would add an Endpoints-to-Host edge that closes a namespace cycle with Mcp (ARCH-CYCLE-001)
        return group;
    }
}
