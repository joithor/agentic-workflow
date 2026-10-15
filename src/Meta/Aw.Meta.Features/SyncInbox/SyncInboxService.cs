using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;

namespace Aw.Meta.Features.SyncInbox;

/// <summary>
/// Reads the transcript past its stored offset and keeps each peer message once. A stored message with no reply, dismissal
/// or deferral is an open reply obligation; an owner question is queued too.
/// </summary>
/// <param name="sync">Reads, stores and queues.</param>
internal sealed class SyncInboxService(InboxSync sync) : IService<SyncInboxRequest, SyncInboxResponse>
{
    public async ValueTask<Result<SyncInboxResponse>> Handle(SyncInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var added = await sync.Run(request.Transcript, cancellationToken).ConfigureAwait(false);
        return added.IsError ? added.Error : new SyncInboxResponse(added.Value);
    }
}
