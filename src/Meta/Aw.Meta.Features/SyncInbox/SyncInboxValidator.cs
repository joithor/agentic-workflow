using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.SyncInbox;

internal sealed class SyncInboxValidator : IValidator<SyncInboxRequest>
{
    public Result<Unit> Validate(SyncInboxRequest value) => Unit.Value;
}
