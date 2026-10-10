using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

/// <summary>The shape every aggregate repository takes: named SQL constants, a mapper, an id binder.</summary>
public sealed class ItemRepository(ISqliteSession session) : SqliteReadRepository<Item, ItemId>(session, ItemMapper.Instance)
{
    internal const string Insert = "INSERT INTO items (id, name, size) VALUES ($id, $name, $size)";

    protected override string GetSql => "SELECT id, name, size FROM items WHERE id = $id";

    protected override string ExistsSql => "SELECT EXISTS (SELECT 1 FROM items WHERE id = $id)";

    protected override void BindId(SqliteCommand command, ItemId id) => command.Bind("$id", id.Value);

    public ValueTask<Result<int>> Add(Item item, CancellationToken cancellationToken) =>
        SqliteQueries.Execute(
            Session,
            new SqlStatement(Insert, c => c.Bind("$id", item.Id.Value).Bind("$name", item.Name).Bind("$size", item.Size)),
            cancellationToken);
}
