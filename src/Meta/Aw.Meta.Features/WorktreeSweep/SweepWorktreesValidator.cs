using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.WorktreeSweep;

internal sealed class SweepWorktreesValidator : IValidator<SweepWorktreesRequest>
{
    public Result<Unit> Validate(SweepWorktreesRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Pr is < 1 ? MetaErrors.Invalid.With("a pull request number from 1 is required") : Unit.Value;
    }
}
