using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.SyncInbox;

/// <summary>
/// Reads the transcript past its stored offset, keeps each peer message once (the store dedupes on the message id) and
/// moves the offset in the same step. A stored message with no reply, dismissal or deferral is an open reply obligation.
/// </summary>
/// <param name="transcripts">Reads the transcript.</param>
/// <param name="messages">Stores the messages and the offset.</param>
/// <param name="clock">Dates a record that carries no timestamp.</param>
internal sealed class SyncInboxService(ITranscriptReader transcripts, IMessageStore messages, TimeProvider clock) : IService<SyncInboxRequest, SyncInboxResponse>
{
    public async ValueTask<Result<SyncInboxResponse>> Handle(SyncInboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var offset = await messages.Offset(request.Transcript, cancellationToken).ConfigureAwait(false);
        if (offset.IsError)
        {
            return offset.Error;
        }

        var chunk = await transcripts.ReadFrom(request.Transcript, offset.Value, cancellationToken).ConfigureAwait(false);
        if (chunk.IsError)
        {
            return chunk.Error;
        }

        var now = clock.GetUtcNow();
        var found = chunk.Value.Lines
            .Select(line => TranscriptRecordParser.Parse(line, now))
            .OfType<PeerMessage>()
            .Select(InboxMessage.Of)
            .ToList();
        var added = await messages.RecordInbound(request.Transcript, chunk.Value.NextOffset, found, cancellationToken).ConfigureAwait(false);
        return added.IsError ? added.Error : new SyncInboxResponse(added.Value);
    }
}
