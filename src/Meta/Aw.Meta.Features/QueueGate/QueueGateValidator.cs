using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.QueueGate;

internal sealed class QueueGateValidator : IValidator<QueueGateRequest>
{
    public Result<Unit> Validate(QueueGateRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Max < 1 ? MetaErrors.Invalid.With("--max must be at least 1")
            : value.Prs.Any(pr => pr < 1) ? MetaErrors.Invalid.With("a pull request number from 1 is required")
            : Unit.Value;
    }
}
