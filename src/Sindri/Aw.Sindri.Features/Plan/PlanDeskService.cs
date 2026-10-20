using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>'line desk step.plan.check|evidence': the plan station's block Steps, run at the desk.</summary>
internal sealed class PlanDeskService : IService<PlanDeskRequest, LineDeskResponse>
{
    public ValueTask<Result<LineDeskResponse>> Handle(PlanDeskRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
