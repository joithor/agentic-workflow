using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.SyncInbox;

internal sealed class SyncInboxService : IService<SyncInboxRequest, SyncInboxResponse>
{
    public ValueTask<Result<SyncInboxResponse>> Handle(SyncInboxRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SyncInbox: make SyncInboxTests pass");
}
