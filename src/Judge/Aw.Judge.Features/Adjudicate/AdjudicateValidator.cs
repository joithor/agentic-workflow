using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.Adjudicate;

internal sealed class AdjudicateValidator : IValidator<AdjudicateRequest>
{
    public Result<Unit> Validate(AdjudicateRequest value) => Unit.Value;
}
