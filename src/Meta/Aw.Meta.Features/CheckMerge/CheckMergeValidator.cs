using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.CheckMerge;

internal sealed class CheckMergeValidator : IValidator<CheckMergeRequest>
{
    public Result<Unit> Validate(CheckMergeRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Pr < 1 ? MetaErrors.Invalid.With("a pull request number from 1 is required") : Unit.Value;
    }
}
