using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.InboxDefer;

internal sealed class DeferInboxService : IService<DeferInboxRequest, DeferInboxResponse>
{
    public ValueTask<Result<DeferInboxResponse>> Handle(DeferInboxRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
