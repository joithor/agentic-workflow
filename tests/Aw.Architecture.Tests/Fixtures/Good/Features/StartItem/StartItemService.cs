using Aw.Architecture.Tests.Fixtures.Good.Core;

namespace Aw.Architecture.Tests.Fixtures.Good.Features.StartItem;

public sealed class StartItemService(IItemStore store)
{
    public Item? Start(string name) => store.Find(name);
}
