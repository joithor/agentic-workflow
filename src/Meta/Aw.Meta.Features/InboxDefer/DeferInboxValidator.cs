using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Features.InboxDefer;

internal sealed class DeferInboxValidator : IValidator<DeferInboxRequest>
{
    public Result<Unit> Validate(DeferInboxRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value.Reason))
        {
            return MetaErrors.Invalid.With("a reason is required");
        }

        // The syntax is checked against a fixed time: a relative time is valid whatever it counts from.
        return DueTime.Parse(value.Until, DateTimeOffset.UnixEpoch) is null
            ? MetaErrors.Invalid.With($"can't read '{value.Until}' as a time; use +30m, +2h, +1d or an ISO 8601 time")
            : Unit.Value;
    }
}
