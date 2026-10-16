using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// The engine part of the needs-you fold (API P11): a root instance's 'workflow.*' rows put or clear its 'park' row. The items projection
/// folds the same event first, so the item row's engine part is the instance as the event left it; this reads it (and the item's title and
/// repo) and never the other way round. A child instance's row and a row of an earlier attempt leave the list alone.
/// </summary>
internal sealed partial class NeedsYouProjection
{
    private async ValueTask<Result<Unit>> Engine(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var payload = events.Payload<WorkflowEventPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        if (recorded.Item is not { } key)
        {
            return Unit.Value;
        }

        var item = await rows.Get<ItemRow>(ViewNames.Items, key, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        // no item row, a child, or an earlier attempt: skipped, not a stall
        return item.Value?.Engine is { } part && string.Equals(part.Address, payload.Value.Instance, StringComparison.Ordinal)
            ? await Settle(rows, (item.Value, part), payload.Value, recorded, cancellationToken).ConfigureAwait(false)
            : Unit.Value;
    }

    private async ValueTask<Result<Unit>> Settle(IViewRows rows, (ItemRow Item, EngineItemPart Part) at, WorkflowEventPayload payload, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var instance = WorkflowInstanceId.Of(InstanceAddress.From(at.Part.Address)).Value;
        var wanted = Wanted(at.Item, at.Part, payload);
        var keepsTakeover = at.Part is { Suspension: SuspensionKind.Takeover, Ended: false };   // a takeover row stays until the handback or the end
        foreach (var prefix in new[] { "park:", "desk:", "takeover:" })
        {
            var id = prefix + instance;
            var outcome = wanted is { } w && string.Equals(w.Prefix, prefix, StringComparison.Ordinal)
                ? await PutEngine(rows, id, w.Build, recorded, cancellationToken).ConfigureAwait(false)
                : keepsTakeover && prefix == "takeover:"
                    ? Unit.Value
                    : await rows.Delete(ViewNames.NeedsYou, id, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome;
            }
        }

        return Unit.Value;
    }

    private (string Prefix, Func<DateTimeOffset, NeedsYouRow> Build)? Wanted(ItemRow item, EngineItemPart part, WorkflowEventPayload payload)
    {
        var address = InstanceAddress.From(part.Address);
        if (part.Ended)
        {
            return null;
        }

        return part.Suspension switch
        {
            SuspensionKind.Park or SuspensionKind.Escalation when payload.Detail?.Delta.Suspension is { } suspension =>
                ("park:", since => NeedsYouRows.ForPark(item, address, suspension, since, scrubber)),
            SuspensionKind.Takeover when string.Equals(payload.Trigger, TriggerNames.TakeoverIdle, StringComparison.Ordinal) =>
                ("takeover:", since => NeedsYouRows.ForIdleTakeover(item, address, since, scrubber)),
            null when part is { RunOpen: true, Runtime: StepRuntime.Desk } => ("desk:", since => NeedsYouRows.ForDeskTask(item, address, since, scrubber)),
            _ => null,
        };
    }

    private static async ValueTask<Result<Unit>> PutEngine(IViewRows rows, string id, Func<DateTimeOffset, NeedsYouRow> build, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var existing = await rows.Get<NeedsYouRowRecord>(ViewNames.NeedsYou, id, cancellationToken).ConfigureAwait(false);
        if (existing.IsError)
        {
            return existing.Error;
        }

        var row = build(existing.Value?.Row.Since ?? recorded.Envelope.OccurredAt);   // the row keeps the time it first appeared
        return await rows.Put(ViewNames.NeedsYou, id, new NeedsYouRowRecord(row, recorded.Sequence), cancellationToken).ConfigureAwait(false);
    }
}
