using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.InboxDismiss;

internal sealed class DismissInboxValidator : IValidator<DismissInboxRequest>
{
    public Result<Unit> Validate(DismissInboxRequest value) => throw new NotSupportedException();
}
