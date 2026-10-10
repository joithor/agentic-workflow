using Aw.Kernel.Data;
using Aw.Architecture.Tests.Fixtures.Good.Core;

namespace Aw.Architecture.Tests.Fixtures.Good.Infrastructure;

internal sealed class SqliteItemStore(ISqliteConnectionFactory connections) : IItemStore
{
    public ISqliteConnectionFactory Connections { get; } = connections;

    public Item? Find(string name) => new ItemRow(name) is var row ? new Item(row.Name) : null;
}
