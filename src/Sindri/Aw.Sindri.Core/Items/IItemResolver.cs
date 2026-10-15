using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>The read port that turns a route's item reference into the item, through the views (API spec §3.1).</summary>
public interface IItemResolver
{
    /// <summary>The item <paramref name="reference"/> names: by id, or by alias through 'item-aliases'. SND-ITEM-404 when there is none, or a session caller may not see it.</summary>
    /// <param name="reference">The id or alias.</param>
    /// <param name="caller">The caller (a session sees only its repo's items).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The item's row, or SND-ITEM-404.</returns>
    public ValueTask<Result<ItemRow>> Resolve(ItemRef reference, CallerContext caller, CancellationToken cancellationToken);
}
