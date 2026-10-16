using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.LaneSend;

internal sealed class LaneSendValidator : IValidator<LaneSendRequest>
{
    public Result<Unit> Validate(LaneSendRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value.Lane))
        {
            return MetaErrors.Invalid.With("a lane is required");
        }

        return string.IsNullOrWhiteSpace(value.Text) ? MetaErrors.Invalid.With("the message text must not be blank") : Unit.Value;
    }
}
