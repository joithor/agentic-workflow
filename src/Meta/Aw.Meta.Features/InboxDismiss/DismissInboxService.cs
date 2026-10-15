using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.InboxDismiss;

/// <summary>Closes a message's reply obligation without a reply, keeping the reason.</summary>
/// <param name="messages">Stores the resolution.</param>
internal sealed class DismissInboxService(IMessageStore messages) : IService<DismissInboxRequest, DismissInboxResponse>
{
    public async ValueTask<Result<DismissInboxResponse>> Handle(DismissInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = await messages.Resolve(request.Id, new MessageResolution(MessageResolutionKind.Dismissed, request.Reason.Trim()), cancellationToken).ConfigureAwait(false);
        return resolved.IsError ? resolved.Error : new DismissInboxResponse(request.Id);
    }
}
