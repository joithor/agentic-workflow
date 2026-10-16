using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Turns the item a context query was asked about into the key the engine reads: a Sindri item id becomes its linked tracker record.</summary>
internal static class ItemKeys
{
    /// <summary>
    /// A Sindri item id ('ITM-...') the caller can see becomes its linked record's key; one with no record yet answers SND-CONTEXT-003
    /// saying so. Anything else (a tracker key, a record key, an id no item has or the caller may not see) goes on unchanged, so the engine
    /// answers an unknown or out-of-scope key the same way for every caller.
    /// </summary>
    /// <param name="item">The item as asked.</param>
    /// <param name="items">Resolves a Sindri item id.</param>
    /// <param name="caller">The caller, or null for an in-process one.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The key to ask the engine for, or SND-CONTEXT-003.</returns>
    internal static async ValueTask<Result<string?>> Resolve(string? item, IItemResolver items, CallerContext? caller, CancellationToken cancellationToken)
    {
        if (item is null || caller is null || !ItemId.TryFrom(item.Trim(), out var id))
        {
            return item;
        }

        var row = await items.Resolve(ItemRef.To(id), caller, cancellationToken).ConfigureAwait(false);
        return row.IsError ? item
            : row.Value.Link is { } record ? record.Value
            : ContextErrors.UnknownAnchor.With($"the item '{id.Value}' has no tracker record linked yet (fix: it is linked once context-sync syncs a record carrying its key; ask by the tracker key meanwhile)");
    }
}
