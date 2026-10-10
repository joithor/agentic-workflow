using Aw.Architecture.Tests.Fixtures.Good.Core;

namespace Aw.Architecture.Tests.Fixtures.Good.Features.ListItems;

public sealed class ListItemsService(IItemStore store)
{
    public IReadOnlyList<Item> List(string name) => store.Find(name) is { } item ? [item] : [];
}
