using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.GetApproveAllPlan;

/// <summary>The approve-all plan (setup-once spec §5.3, §7.2): the engine's plan for the shipped recipes with the views' freshness, once a profile is approved.</summary>
/// <param name="plans">Reads the engine's plan, refusing before a profile is approved.</param>
/// <param name="views">Where the views stand.</param>
internal sealed class GetApproveAllPlanService(ApproveAllPlanReader plans, IProjectorStatus views) : IService<GetApproveAllPlanRequest, ApproveAllPlan>
{
    public async ValueTask<Result<ApproveAllPlan>> Handle(GetApproveAllPlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = await plans.Read(cancellationToken).ConfigureAwait(false);
        return plan.IsError ? plan.Error : ApproveAllPlan.Of(plan.Value, views.Current.AsOf);
    }
}
