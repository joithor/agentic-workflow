using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The one check for "this item ended by its workflow" (API P11, review run 2 I4): an item whose root instance ends closes as a projection
/// only (no 'item.state-changed' row, decision Q6), and what a human close does (void the pending decisions and open requests, clear the
/// decision rows) must follow the item row reaching a closed state. The requests projection folds before the items one, so it asks
/// <see cref="EndsItem"/> with the row as it is; the needs-you projection folds after, so it asks <see cref="IsEnded"/> of the row as written.
/// </summary>
internal static class EngineClosure
{
    /// <summary>True when the item row, as the items projection wrote it, is closed.</summary>
    /// <param name="row">The item row after the event.</param>
    /// <returns>True when closed.</returns>
    public static bool IsEnded(ItemRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return ItemStates.IsClosed(row.State);
    }

    /// <summary>True when the item row, once the items projection folds this 'workflow.*' row into it, is closed.</summary>
    /// <param name="row">The item row before the event.</param>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>True when closed.</returns>
    public static bool EndsItem(ItemRow row, string type, WorkflowEventPayload payload, LedgerEvent recorded)
        => IsEnded(ItemsProjection.Fold(row, type, payload, recorded));
}
