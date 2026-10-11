using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.PruneDecisionDetails;

internal sealed class PruneDecisionDetailsValidator : IValidator<PruneDecisionDetailsRequest>
{
    public Result<Unit> Validate(PruneDecisionDetailsRequest value) => Unit.Value;
}
