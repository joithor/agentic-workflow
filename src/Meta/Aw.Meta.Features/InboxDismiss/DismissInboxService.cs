using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.InboxDismiss;

internal sealed class DismissInboxService : IService<DismissInboxRequest, DismissInboxResponse>
{
    public ValueTask<Result<DismissInboxResponse>> Handle(DismissInboxRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
