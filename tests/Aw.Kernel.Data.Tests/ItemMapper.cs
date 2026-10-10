using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

public sealed class ItemMapper : IRowMapper<Item>
{
    public static readonly ItemMapper Instance = new();

    public Item Map(SqliteDataReader reader) => new(new ItemId(reader.GetString(0)), reader.GetString(1), reader.GetInt64(2));
}
