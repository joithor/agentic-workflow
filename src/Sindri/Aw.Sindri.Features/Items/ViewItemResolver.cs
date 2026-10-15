using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Items;

/// <summary>Resolves an item reference through the views: an alias through 'item-aliases', then the id in 'items', then the caller's repo scope.</summary>
internal sealed class ViewItemResolver : IItemResolver
{
    /// <summary>Resolves through <paramref name="views"/>.</summary>
    /// <param name="views">The views reader.</param>
    public ViewItemResolver(IViewReader views)
    {
        ArgumentNullException.ThrowIfNull(views);
    }

    public ValueTask<Result<ItemRow>> Resolve(ItemRef reference, CallerContext caller, CancellationToken cancellationToken)
        => throw new NotSupportedException("ViewItemResolver: P3 unit D makes ViewItemResolverTests pass");
}
