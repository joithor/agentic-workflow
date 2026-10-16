using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.QueueDrop;

internal sealed class QueueDropValidator : IValidator<QueueDropRequest>
{
    public Result<Unit> Validate(QueueDropRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Pr <= 0)
        {
            return MetaErrors.Invalid.With("the pull request must be a positive number");
        }

        return string.IsNullOrWhiteSpace(value.Reason) ? MetaErrors.Invalid.With("a reason is required") : Unit.Value;
    }
}
