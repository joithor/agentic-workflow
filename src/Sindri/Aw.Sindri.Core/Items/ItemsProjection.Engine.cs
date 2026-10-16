using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>The engine part of the items fold: a root instance's 'workflow.*' rows (API P11).</summary>
internal sealed partial class ItemsProjection
{
    /// <summary>
    /// The row after a root instance's 'workflow.*' row (API P11): the engine part and the state it reads as. Projection-only (no version bump,
    /// no event of its own). An item with no row, a child's row and a row of an earlier attempt leave things as they are.
    /// </summary>
    /// <param name="row">The row so far.</param>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The new row; the same row when the event leaves the item alone.</returns>
    public static ItemRow Fold(ItemRow row, string type, WorkflowEventPayload payload, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(recorded);
        var started = string.Equals(type, EventTypes.WorkflowStarted, StringComparison.Ordinal);
        if (row.State is ItemState.Done or ItemState.Cancelled && !started && EngineClosure.LiveRoot(row) is null)
        {
            return row;   // closed by a human, or by the instance this part already reads; an earlier root that is still live keeps moving
        }

        var part = EngineItemStates.Next(row.Engine, type, payload);
        if (part is null)
        {
            return row;
        }

        var state = EngineItemStates.StateOf(part);
        var at = recorded.Envelope.OccurredAt;
        return row with
        {
            State = state,
            Step = part.State,
            Instance = part.Address,
            Engine = part,
            Since = state == row.State ? row.Since : at,
            WaitingOn = null,
            UpdatedAt = at,
            Sequence = recorded.Sequence,
        };
    }

    private async ValueTask<Result<Unit>> Engine(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var payload = events.Payload<WorkflowEventPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        if (recorded.Item is not { } item)
        {
            return Unit.Value;
        }

        var row = await rows.Get<ItemRow>(ViewNames.Items, item, cancellationToken).ConfigureAwait(false);
        if (row.IsError || row.Value is null)
        {
            return row.IsError ? row.Error : Unit.Value;   // an instance on an item the views have no row for: skipped, not a stall
        }

        var next = Fold(row.Value, recorded.Envelope.Type, payload.Value, recorded);
        return ReferenceEquals(next, row.Value) ? Unit.Value : await rows.Put(ViewNames.Items, item, next, cancellationToken).ConfigureAwait(false);
    }
}
