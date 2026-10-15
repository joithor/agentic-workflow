using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.InboxDefer;

internal sealed class DeferInboxValidator : IValidator<DeferInboxRequest>
{
    public Result<Unit> Validate(DeferInboxRequest value) => throw new NotSupportedException();
}
