using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>
/// Reads the ledger after the cursor to its head and keeps the instance's events (<see cref="InstanceEvents.Belongs(LedgerEvent, InstanceAddress)"/>):
/// Temporal's own history is never read (engine spec §5.6). An opaque id is matched against the addresses in the ledger's own
/// keys, so an ended instance's trail reads after Temporal has dropped it (visibility lag, retention); an address or bare item
/// is resolved through the directory. A first read (no cursor) that finds nothing checks that the instance exists, so a
/// mistyped address is SND-WF-001 rather than 'no events'.
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
        var selected = await Select(request.Instance, cancellationToken).ConfigureAwait(false);
        if (selected.IsError)
        {
            return selected.Error;
        }

        var (belongs, address) = selected.Value;
        var found = new List<InstanceEventView>();
        var cursor = Math.Max(request.After, 0);
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, Page, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            found.AddRange(page.Value.Where(belongs).Select(View));
            if (page.Value.Count < Page)
            {
                if (found.Count == 0 && request.After <= 0)
                {
                    var exists = await Exists(request.Instance, address, cancellationToken).ConfigureAwait(false);
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

    /// <summary>Which ledger events are the instance's: by the ledger's own keys for an id, by the resolved address otherwise (whose address is then known).</summary>
    private async ValueTask<Result<(Func<LedgerEvent, bool> Belongs, InstanceAddress? Address)>> Select(string reference, CancellationToken cancellationToken)
    {
        if (WorkflowInstanceId.TryFrom(reference, out var id))
        {
            return (e => InstanceEvents.Belongs(e, id), null);
        }

        var resolved = await directory.Resolve(reference, cancellationToken).ConfigureAwait(false);
        return resolved.IsError
            ? resolved.Error
            : (e => InstanceEvents.Belongs(e, resolved.Value), resolved.Value);
    }

    /// <summary>An instance with no rows yet exists in Temporal, else the reference is SND-WF-001.</summary>
    private async ValueTask<Result<Unit>> Exists(string reference, InstanceAddress? address, CancellationToken cancellationToken)
    {
        if (address is null)
        {
            var resolved = await directory.Resolve(reference, cancellationToken).ConfigureAwait(false);
            if (resolved.IsError)
            {
                return resolved.Error;
            }

            address = resolved.Value;
        }

        var exists = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        return exists.IsError ? exists.Error : Unit.Value;
    }

    private InstanceEventView View(LedgerEvent recorded)
    {
        var fields = events.Payload<InstanceEventFields>(recorded);
        var read = fields.IsError ? InstanceEventFields.None : fields.Value;
        return new InstanceEventView(recorded.Sequence, recorded.Envelope.Type, recorded.Envelope.OccurredAt, recorded.Envelope.Actor,
            string.Equals(read.Trigger, TriggerNames.StepFailed, StringComparison.Ordinal) ? read with { Reason = FailureReason(recorded.Envelope.Payload) } : read);
    }

    /// <summary>The 'reason' in a step.failed payload's detail; null when the payload has none.</summary>
    private static string? FailureReason(string payload)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == System.Text.Json.JsonValueKind.Object
                && detail.TryGetProperty("reason", out var reason) && reason.ValueKind == System.Text.Json.JsonValueKind.String
                ? reason.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
