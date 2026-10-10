using Aw.Architecture.Tests.Fixtures.Good.Core;
using Aw.Kernel.Data;

namespace Aw.Architecture.Tests.Fixtures.Good.Infrastructure;

internal sealed class SqliteItemStore(ISqliteConnectionFactory connections) : IItemStore
{
    public ISqliteConnectionFactory Connections { get; } = connections;

    public Item? Find(string name) => new ItemRow(name) is var row ? new Item(row.Name) : null;
}
