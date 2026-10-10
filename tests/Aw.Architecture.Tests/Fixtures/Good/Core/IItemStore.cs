namespace Aw.Architecture.Tests.Fixtures.Good.Core;

public interface IItemStore
{
    public Item? Find(string name);
}
