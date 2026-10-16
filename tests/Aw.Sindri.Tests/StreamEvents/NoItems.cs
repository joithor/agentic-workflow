using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.StreamEvents;

/// <summary>An item resolver that knows no item, for the tests that don't name one.</summary>
internal sealed class NoItems : IItemResolver
{
    public ValueTask<Result<ItemRow>> Resolve(ItemRef reference, CallerContext caller, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<ItemRow>>(SindriErrors.ItemNotFound.With("no item '" + reference + "'"));
}
