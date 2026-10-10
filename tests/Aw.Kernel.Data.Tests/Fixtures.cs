using Aw.Kernel.Persistence;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

public readonly record struct ItemId(string Value);

public sealed record Item(ItemId Id, string Name, long Size);

public sealed class ItemMapper : IRowMapper<Item>
{
    public static readonly ItemMapper Instance = new();

    public Item Map(SqliteDataReader reader) => new(new ItemId(reader.GetString(0)), reader.GetString(1), reader.GetInt64(2));
}

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

/// <summary>A clock the tests set by hand.</summary>
public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public static class Schema
{
    public static readonly Migration Items = new(1, "items", "CREATE TABLE items (id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, size INTEGER NOT NULL);");

    public static readonly Migration Tags = new(2, "tags", "CREATE TABLE tags (item_id TEXT NOT NULL REFERENCES items (id), tag TEXT NOT NULL);");

    public static readonly Migration Notes = new(3, "notes", "CREATE TABLE notes (id INTEGER PRIMARY KEY, body TEXT NOT NULL);");

    public static async Task CreateItems(TempDatabase db)
    {
        var report = await new MigrationRunner(db.Factory(), new FixedClock(DateTimeOffset.UnixEpoch))
            .Migrate([Items, Tags], TestContext.Current.CancellationToken);
        Assert.True(report.IsSuccess);
    }
}
