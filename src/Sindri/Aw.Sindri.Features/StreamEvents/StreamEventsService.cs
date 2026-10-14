using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>
/// Reads events after the cursor from the ledger. With a filter (S1-D6) it reads on, page by page, until it has
/// <see cref="StreamEventsRequest.Max"/> matching events or reaches the head, and reports how far it read.
/// </summary>
/// <param name="ledger">The ledger.</param>
internal sealed class StreamEventsService(ILedgerReader ledger) : IService<StreamEventsRequest, StreamEventsResponse>
{
    public async ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var max = Math.Clamp(request.Max, 1, 500);
        var cursor = Math.Max(request.After, 0);
        var kept = new List<EventView>();
        long? scanned = null;
        while (kept.Count < max)
        {
            var page = await ledger.ReadAfter(cursor, max, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var e in page.Value)
            {
                scanned = cursor = e.Sequence;
                if (Keeps(request, e))
                {
                    kept.Add(View(e));
                    if (kept.Count == max)
                    {
                        break;
                    }
                }
            }

            if (page.Value.Count < max)
            {
                break;
            }
        }

        return new StreamEventsResponse(kept) { Scanned = scanned };
    }

    private static bool Keeps(StreamEventsRequest request, LedgerEvent e) =>
        (request.Item is not { } item || string.Equals(e.Item, item.Value, StringComparison.Ordinal))
        && (request.Instance is not { } instance || InstanceEvents.Belongs(e, instance));

    private static EventView View(LedgerEvent e) =>
        new(e.Sequence, e.Envelope.Type, e.Envelope.OccurredAt, e.Envelope.Actor, e.Envelope.Tenant, e.Envelope.Ring, e.Epoch, e.Envelope.Payload);
}
