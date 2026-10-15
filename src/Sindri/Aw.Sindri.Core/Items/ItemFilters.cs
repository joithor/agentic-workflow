using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Items;

/// <summary>The item an event read names (API spec §3.1, §5.2): an id or tracker alias, resolved for the caller once per read.</summary>
public static class ItemFilters
{
    /// <summary>
    /// The id <paramref name="reference"/> names, or null when the read has no item filter; SND-ITEM-404 when there is no such item
    /// or a session may not see it, SND-CLI-003 when a filter comes without a caller.
    /// </summary>
    /// <param name="reference">The reference, or null.</param>
    /// <param name="items">Resolves a reference.</param>
    /// <param name="callers">The caller, whose repo scope applies.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The id, or null.</returns>
    public static async ValueTask<Result<ItemId?>> Resolve(ItemRef? reference, IItemResolver items, ICallerContextAccessor callers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(callers);
        if (reference is not { } named)
        {
            return (ItemId?)null;
        }

        if (callers.Current is not { } caller)
        {
            return SindriErrors.InvalidInput.With("an item filter needs a caller");
        }

        var row = await items.Resolve(named, caller, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : row.Value.Id;
    }
}
