using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.MergeRun;

internal sealed class MergeRunValidator : IValidator<MergeRunRequest>
{
    public Result<Unit> Validate(MergeRunRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Pr < 1 ? MetaErrors.Invalid.With("a pull request number from 1 is required") : Unit.Value;
    }
}
