using Aw.Evidence.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityPlan;

internal sealed class ParityPlanValidator : IValidator<ParityPlanRequest>
{
    public Result<Unit> Validate(ParityPlanRequest value) =>
        value.RunDirectory.StartsWith('/') ? Unit.Value : EvidenceErrors.Invalid.With("the run directory must be absolute");
}
