// R8 probe: an intercepted Dapper query works under AOT; a generic helper whose T the generator can't see
// falls back to Dapper's runtime (reflection/IL-emit) path. Does that fail at build time or at runtime?
using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

[module: DapperAot]

using var conn = new SqliteConnection("Data Source=:memory:");
conn.Open();
conn.Execute("CREATE TABLE items (id TEXT PRIMARY KEY, name TEXT NOT NULL, size INTEGER NOT NULL)");
conn.Execute("INSERT INTO items (id, name, size) VALUES (@Id, @Name, @Size)", new Row { Id = "a", Name = "alpha", Size = 1 });

var direct = conn.Query<Row>("SELECT id AS Id, name AS Name, size AS Size FROM items").AsList();
Console.WriteLine($"dapper-fallback: intercepted Query<Row>: {direct.Count} row(s), first {direct[0].Name}");

try
{
    var viaGeneric = Repo.All<Row>(conn, "SELECT id AS Id, name AS Name, size AS Size FROM items");
    Console.WriteLine($"dapper-fallback: generic Query<T>: {viaGeneric.Count} row(s)");
}
catch (Exception e)
{
    Console.WriteLine($"dapper-fallback: generic Query<T> threw {e.GetType().Name}: {e.Message.Split('\n')[0]}");
    return 1;
}
return 0;

public sealed class Row
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Size { get; set; }
}

static class Repo
{
    // The generator sees Query<T> with an open T, so it can't generate a reader for it.
    public static List<T> All<T>(IDbConnection c, string sql) => c.Query<T>(sql).AsList();
}
