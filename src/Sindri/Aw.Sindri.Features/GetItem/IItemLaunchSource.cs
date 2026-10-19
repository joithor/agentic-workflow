using Aw.Kernel;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetItem;

/// <summary>Where an item's launch block comes from.</summary>
internal interface IItemLaunchSource
{
    /// <summary>The block for an item.</summary>
    /// <param name="item">The item row.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The block, or why a projection couldn't be read.</returns>
    public ValueTask<Result<ItemLaunch>> For(ItemRow item, CancellationToken cancellationToken);
}
