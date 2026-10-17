using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.QueueDrop;

/// <summary>
/// Removes a pull request's entry from the merge queue and logs why. The ready messages stay in the inbox; the drop is remembered
/// with its time, so only a ready message sent after it queues the pull request again.
/// </summary>
/// <param name="messages">The inbound messages.</param>
/// <param name="gates">Tells which pull requests are merged.</param>
/// <param name="values">Keeps the drops and the pull requests the watch saw end.</param>
/// <param name="log">The running log.</param>
/// <param name="reservation">Computes the heavy-slot reservation again once the entry has left the queue.</param>
/// <param name="clock">Dates the drop.</param>
internal sealed class QueueDropService(IMessageStore messages, IGateStore gates, IKeyValueStore values, IRunningLog log, HeavyReservation reservation, TimeProvider clock) : IService<QueueDropRequest, QueueDropResponse>
{
    public async ValueTask<Result<QueueDropResponse>> Handle(QueueDropRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stored = await values.Get(KvKeys.QueueDropped, cancellationToken).ConfigureAwait(false);
        var queued = stored.IsError ? Results.Failure<bool>(stored.Error) : await InQueue(request.Pr, QueueDrops.Parse(stored.Value), cancellationToken).ConfigureAwait(false);
        if (queued.IsError)
        {
            return queued.Error;
        }

        if (!queued.Value)
        {
            return MetaErrors.QueueEntryNotFound.With(string.Create(CultureInfo.InvariantCulture, $"no ready message for #{request.Pr} is in the queue"));
        }

        var now = clock.GetUtcNow();
        var saved = await values.Set(KvKeys.QueueDropped, QueueDrops.With(stored.Value, request.Pr, now), cancellationToken).ConfigureAwait(false);
        var line = LogLine.Event(LogKind.Note, now, string.Create(CultureInfo.InvariantCulture, $"queue-drop #{request.Pr}: {request.Reason}"));
        var appended = saved.IsError ? Results.Failure<Unit>(saved.Error) : await log.Append(line, cancellationToken).ConfigureAwait(false);
        if (saved.IsSuccess)
        {
            // Advisory: the queue is shorter now, so the reservation may drop; failing to keep it must not undo the drop.
            _ = await reservation.Apply(cancellationToken).ConfigureAwait(false);
        }

        return appended.IsError ? appended.Error : new QueueDropResponse(request.Pr, line);
    }

    // The same selection as `status`: a merged pull request, or one the watch saw merged or closed, is not in the queue.
    private async ValueTask<Result<bool>> InQueue(int pr, IReadOnlyDictionary<int, DateTimeOffset> drops, CancellationToken cancellationToken)
    {
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        var ended = await values.Get(KvKeys.EndedPrs, cancellationToken).ConfigureAwait(false);
        var merge = await gates.FindMerge(pr, cancellationToken).ConfigureAwait(false);
        if (inbound.IsError || ended.IsError || merge.IsError)
        {
            return inbound.IsError ? inbound.Error : ended.IsError ? ended.Error : merge.Error;
        }

        return merge.Value is null
            && !EndedPrs.Parse(ended.Value).Contains(pr)
            && inbound.Value.Any(m => Queues(m, pr, drops));
    }

    private static bool Queues(InboxMessage message, int pr, IReadOnlyDictionary<int, DateTimeOffset> drops) =>
        message.Class is { Kind: MessageKind.Ready } ready && ready.Pr == pr && (!drops.TryGetValue(pr, out var at) || message.At > at);
}
