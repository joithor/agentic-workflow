using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.InboxDismiss;

internal sealed class DismissInboxValidator : IValidator<DismissInboxRequest>
{
    public Result<Unit> Validate(DismissInboxRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.IsNullOrWhiteSpace(value.Reason) ? MetaErrors.Invalid.With("a reason is required") : Unit.Value;
    }
}
