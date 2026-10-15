using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.StopCheck;

internal sealed class StopCheckValidator : IValidator<StopCheckRequest>
{
    public Result<Unit> Validate(StopCheckRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value.Transcript))
        {
            return MetaErrors.Invalid.With("a transcript path is required");
        }

        return value.MaxBlocks < 1 ? MetaErrors.Invalid.With("the block limit must be at least 1") : Unit.Value;
    }
}
