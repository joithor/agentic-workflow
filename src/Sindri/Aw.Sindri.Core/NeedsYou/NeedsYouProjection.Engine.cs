using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>
/// The engine part of the needs-you fold (API P11): a root instance's 'workflow.*' rows put or clear its 'park' row. The items projection
/// folds the same event first, so the item row's engine part is the instance as the event left it; this reads it (and the item's title and
/// repo) and never the other way round. A child instance's row leaves the list alone; each root of the item (the latest, or an earlier one still live) settles its own rows, and a root the item no longer holds has ended, so its rows clear.
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

        if (!EngineItemStates.IsRoot(payload.Value.Instance))
        {
            return Unit.Value;   // a child instance's row
        }

        var item = await rows.Get<ItemRow>(ViewNames.Items, key, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        // no item row: skipped, not a stall. Otherwise the row is settled by the root's own part (the latest root's, or an earlier live one's); a root the item no longer holds ended, so its rows clear
        if (item.Value is { } ended && EngineClosure.IsEnded(ended))
        {
            var voided = await ClearDecisions(rows, key, cancellationToken).ConfigureAwait(false);   // the items projection already folded this row: the item ended, so what it waited on is void (as a human close does)
            if (voided.IsError)
            {
                return voided;
            }
        }

        return item.Value is { } row
            ? await Settle(rows, (row, EngineItemStates.PartOf(row.Engine, payload.Value.Instance)), payload.Value, recorded, cancellationToken).ConfigureAwait(false)
            : Unit.Value;
    }

    private async ValueTask<Result<Unit>> Settle(IViewRows rows, (ItemRow Item, EngineItemPart? Part) at, WorkflowEventPayload payload, LedgerEvent recorded, CancellationToken cancellationToken)
    {
        var instance = WorkflowInstanceId.Of(InstanceAddress.From(payload.Instance)).Value;
        var wanted = at.Part is { } part ? Wanted(at.Item, part, payload) : null;
        var keeps = at.Part is { Ended: false } live ? Kept(live, payload) : null;   // a takeover row stays until the handback or the end; a park row stays across a row that doesn't say
        foreach (var prefix in new[] { "park:", "desk:", "takeover:" })
        {
            var id = prefix + instance;
            var outcome = wanted is { } w && string.Equals(w.Prefix, prefix, StringComparison.Ordinal)
                ? await PutEngine(rows, id, w.Build, recorded, cancellationToken).ConfigureAwait(false)
                : string.Equals(keeps, prefix, StringComparison.Ordinal)
                    ? Unit.Value
                    : await rows.Delete(ViewNames.NeedsYou, id, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome;
            }
        }

        return Unit.Value;
    }

    /// <summary>The row prefix that stays as it is: a takeover's while the instance is taken over, a park's or escalation's when this row carries no detail to say what changed.</summary>
    private static string? Kept(EngineItemPart part, WorkflowEventPayload payload) => part.Suspension switch
    {
        SuspensionKind.Takeover => "takeover:",
        SuspensionKind.Park or SuspensionKind.Escalation when payload.Detail is null => "park:",
        _ => null,
    };

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
                ("park:", since => NeedsYouRows.ForPark(item, address, suspension, since, scrubber, payload.Detail?.Reason)),
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
