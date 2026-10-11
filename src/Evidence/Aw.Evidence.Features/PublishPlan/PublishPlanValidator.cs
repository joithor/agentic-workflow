using Aw.Evidence.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.PublishPlan;

internal sealed class PublishPlanValidator : IValidator<PublishPlanRequest>
{
    public Result<Unit> Validate(PublishPlanRequest value) =>
        value.RunDirectory.StartsWith('/') ? Unit.Value : EvidenceErrors.Invalid.With("the run directory must be absolute");
}
