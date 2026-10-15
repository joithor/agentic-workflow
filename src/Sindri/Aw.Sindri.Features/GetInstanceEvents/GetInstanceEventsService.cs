using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>
/// Reads the ledger after the cursor to its head and keeps the instance's events (<see cref="InstanceEvents.Belongs"/>):
/// Temporal's own history is never read (engine spec §5.6). A first read (no cursor) that finds nothing checks that the
/// instance exists, so a mistyped address is SND-WF-001 rather than 'no events'.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads each event's payload.</param>
/// <param name="directory">Resolves the reference to the instance's address.</param>
/// <param name="runtime">The existence check for an instance with no events.</param>
internal sealed class GetInstanceEventsService(ILedgerReader ledger, EventFactory events, IInstanceDirectory directory, IInstanceReader runtime) : IService<GetInstanceEventsRequest, GetInstanceEventsResponse>
{
    private const int Page = 500;

    public async ValueTask<Result<GetInstanceEventsResponse>> Handle(GetInstanceEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var resolved = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var address = resolved.Value;
        var found = new List<InstanceEventView>();
        var cursor = Math.Max(request.After, 0);
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            found.AddRange(page.Value.Where(e => InstanceEvents.Belongs(e, address)).Select(View));
            if (page.Value.Count < Page)
            {
                if (found.Count == 0 && request.After <= 0)
                {
                    var exists = await runtime.Get(address, cancellationToken).ConfigureAwait(false);
                    if (exists.IsError)
                    {
                        return exists.Error;
                    }
                }

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
