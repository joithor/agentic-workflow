using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.LaneRecycle;

internal sealed class LaneRecycleValidator : IValidator<LaneRecycleRequest>
{
    public Result<Unit> Validate(LaneRecycleRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value.Lane))
        {
            return MetaErrors.Invalid.With("a lane is required");
        }

        return string.IsNullOrWhiteSpace(value.Digest) ? MetaErrors.Invalid.With("--digest <path> is required") : Unit.Value;
    }
}
