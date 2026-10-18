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

        if (string.IsNullOrWhiteSpace(value.Digest) || value.Digest.Contains('\0', StringComparison.Ordinal))
        {
            return MetaErrors.Invalid.With("--digest <path> is required");
        }

        return Path.IsPathFullyQualified(value.Digest) ? Unit.Value : MetaErrors.Invalid.With("--digest must be an absolute path");
    }
}
