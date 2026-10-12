using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>
/// Reads the ledger after the cursor to its head and keeps the instance's events (<see cref="InstanceEvents.Belongs"/>):
/// Temporal's own history is never read (engine spec §5.6).
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each event's payload.</param>
internal sealed class GetInstanceEventsService(ILedgerReader ledger, EventFactory events) : IService<GetInstanceEventsRequest, GetInstanceEventsResponse>
{
    private const int Page = 500;

    public async ValueTask<Result<GetInstanceEventsResponse>> Handle(GetInstanceEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = new List<InstanceEventView>();
        var cursor = Math.Max(request.After, 0);
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            found.AddRange(page.Value.Where(e => InstanceEvents.Belongs(e, request.Address)).Select(View));
            if (page.Value.Count < Page)
            {
                return new GetInstanceEventsResponse(found, page.Value.Count > 0 ? page.Value[^1].Sequence : cursor);
            }

            cursor = page.Value[^1].Sequence;
        }
    }

    private InstanceEventView View(LedgerEvent recorded)
    {
        var fields = events.Payload<InstanceEventFields>(recorded);
        return new InstanceEventView(recorded.Sequence, recorded.Envelope.Type, recorded.Envelope.OccurredAt, recorded.Envelope.Actor,
            fields.IsError ? InstanceEventFields.None : fields.Value);
    }
}
