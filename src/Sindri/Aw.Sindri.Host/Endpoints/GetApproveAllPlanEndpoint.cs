using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.GetApproveAllPlan;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/workflow/recipes/approve-all (setup-once spec §7.2; query): what one hold would approve. 'approve-all' has no '@', so it is never read as an '{id}@{hash}' route.</summary>
internal static class GetApproveAllPlanEndpoint
{
    internal static RouteGroupBuilder MapGetApproveAllPlan(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/recipes/approve-all", static async (IService<GetApproveAllPlanRequest, ApproveAllPlan> service, CancellationToken ct)
            => (await service.Handle(new GetApproveAllPlanRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
