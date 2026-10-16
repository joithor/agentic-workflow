using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.InboxDefer;

/// <summary>Snoozes a message's reply obligation; it reopens when the time comes.</summary>
/// <param name="messages">Stores the resolution.</param>
/// <param name="clock">What a relative time counts from.</param>
internal sealed class DeferInboxService(IMessageStore messages, TimeProvider clock) : IService<DeferInboxRequest, DeferInboxResponse>
{
    public async ValueTask<Result<DeferInboxResponse>> Handle(DeferInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (DueTime.Parse(request.Until, clock.GetUtcNow()) is not { } until)
        {
            return MetaErrors.Invalid.With($"can't read '{request.Until}' as a time; use +30m, +2h, +1d or an ISO 8601 time");
        }

        var resolved = await messages.Resolve(request.Id, new MessageResolution(MessageResolutionKind.Deferred, request.Reason.Trim(), until), cancellationToken).ConfigureAwait(false);
        return resolved.IsError ? resolved.Error : new DeferInboxResponse(request.Id, until);
    }
}
