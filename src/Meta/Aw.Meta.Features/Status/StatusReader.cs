using Aw.Kernel;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Status;

/// <summary>Reads the parts of the status.</summary>
/// <param name="reader">The open obligations.</param>
/// <param name="messages">The ready messages.</param>
/// <param name="gates">The gate runs and the latest merge.</param>
/// <param name="owner">The owner's queue.</param>
/// <param name="lanes">The lane map.</param>
internal sealed class StatusReader(ObligationReader reader, IMessageStore messages, IGateStore gates, IOwnerStore owner, ILaneStore lanes)
{
    internal async ValueTask<Result<StatusResponse>> Read(string metaLane, DateTimeOffset now, IReadOnlySet<int> ended, CancellationToken cancellationToken)
    {
        var open = await reader.Open(metaLane, now, cancellationToken).ConfigureAwait(false);
        var queue = await owner.List(includeAnswered: false, cancellationToken).ConfigureAwait(false);
        var map = await lanes.Current(cancellationToken).ConfigureAwait(false);
        var ready = await MergeQueue(ended, cancellationToken).ConfigureAwait(false);
        if (open.IsError || queue.IsError || map.IsError || ready.IsError)
        {
            return open.IsError ? open.Error : queue.IsError ? queue.Error : map.IsError ? map.Error : ready.Error;
        }

        return new StatusResponse(open.Value, ready.Value, queue.Value, map.Value);
    }

    private async ValueTask<Result<IReadOnlyList<QueuedPr>>> MergeQueue(IReadOnlySet<int> ended, CancellationToken cancellationToken)
    {
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        if (inbound.IsError)
        {
            return inbound.Error;
        }

        var queued = new List<QueuedPr>();
        foreach (var (pr, from) in Ready(inbound.Value, ended))
        {
            var merge = await gates.FindMerge(pr, cancellationToken).ConfigureAwait(false);
            if (merge.IsError)
            {
                return merge.Error;
            }

            if (merge.Value is not null)
            {
                continue;
            }

            var gate = await gates.Latest(pr, cancellationToken).ConfigureAwait(false);
            if (gate.IsError)
            {
                return gate.Error;
            }

            queued.Add(new QueuedPr(pr, from, gate.Value?.Verdict.ToString() ?? "none"));
        }

        return queued;
    }

    // The latest "ready #n" message per pull request number, lowest number first, leaving out those GitHub shows merged or closed (the caller's set).
    private static IEnumerable<(int Pr, string From)> Ready(IReadOnlyList<InboxMessage> inbound, IReadOnlySet<int> ended) =>
        inbound
            .Where(m => m.Class is { Kind: MessageKind.Ready, Pr: { } pr } && !ended.Contains(pr))
            .GroupBy(m => m.Class.Pr.GetValueOrDefault())
            .OrderBy(group => group.Key)
            .Select(group => group.MaxBy(m => m.At))
            .OfType<InboxMessage>()
            .Select(m => (m.Class.Pr.GetValueOrDefault(), m.FromName.Length > 0 ? m.FromName : m.From));
}
