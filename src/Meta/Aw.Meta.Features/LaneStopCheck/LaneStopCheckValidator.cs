using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.LaneStopCheck;

internal sealed class LaneStopCheckValidator : IValidator<LaneStopCheckRequest>
{
    public Result<Unit> Validate(LaneStopCheckRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.MaxBlocks < 1 ? MetaErrors.Invalid.With("the block limit must be at least 1") : Unit.Value;
    }
}
