using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.QueueDrop;

/// <summary>
/// Removes a pull request's entry from the merge queue and logs why. The ready messages stay in the inbox; the drop is remembered
/// with its time, so only a ready message sent after it queues the pull request again.
/// </summary>
/// <param name="messages">The inbound messages.</param>
/// <param name="values">Keeps the drops.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the drop.</param>
internal sealed class QueueDropService(IMessageStore messages, IKeyValueStore values, IRunningLog log, TimeProvider clock) : IService<QueueDropRequest, QueueDropResponse>
{
    public async ValueTask<Result<QueueDropResponse>> Handle(QueueDropRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        var stored = await values.Get(KvKeys.QueueDropped, cancellationToken).ConfigureAwait(false);
        if (inbound.IsError || stored.IsError)
        {
            return inbound.IsError ? inbound.Error : stored.Error;
        }

        var drops = QueueDrops.Parse(stored.Value);
        var queued = inbound.Value.Any(m => m.Class is { Kind: MessageKind.Ready } c && c.Pr == request.Pr && (!drops.TryGetValue(request.Pr, out var at) || m.At > at));
        if (!queued)
        {
            return MetaErrors.QueueEntryNotFound.With(string.Create(CultureInfo.InvariantCulture, $"no ready message for #{request.Pr} is in the queue"));
        }

        var now = clock.GetUtcNow();
        var line = LogLine.Event(LogKind.Note, now, string.Create(CultureInfo.InvariantCulture, $"queue-drop #{request.Pr}: {request.Reason}"));
        var appended = await log.Append(line, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var saved = await values.Set(KvKeys.QueueDropped, QueueDrops.With(stored.Value, request.Pr, now), cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new QueueDropResponse(request.Pr, line);
    }
}
