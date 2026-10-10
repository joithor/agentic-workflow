// R8 variant (c): Dapper with the Dapper.AOT source generator (interceptors replace reflection-based mapping).
using Dapper;
using Microsoft.Data.Sqlite;
using Spike.R8;

[module: DapperAot]

return Scenario.Run("dapperaot", args.Length > 0 ? args[0] : "dapperaot.db", path => new DapperItemStore(path));

sealed class DapperItemStore : IItemStore
{
    readonly SqliteConnection _conn;

    public DapperItemStore(string path)
    {
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        _conn.Execute("PRAGMA journal_mode = wal");
    }

    public string JournalMode() => _conn.QuerySingle<string>("PRAGMA journal_mode");
    public void CreateSchema() => _conn.Execute("CREATE TABLE IF NOT EXISTS items (id TEXT PRIMARY KEY, name TEXT NOT NULL, size INTEGER NOT NULL)");
    public void Insert(Item i) => _conn.Execute("INSERT INTO items (id, name, size) VALUES (@Id, @Name, @Size)", i);
    public void Update(Item i) => _conn.Execute("UPDATE items SET name = @Name, size = @Size WHERE id = @Id", i);
    public void Delete(string id) => _conn.Execute("DELETE FROM items WHERE id = @id", new { id });
    public Item? Get(string id) => _conn.QuerySingleOrDefault<Item>("SELECT id AS Id, name AS Name, size AS Size FROM items WHERE id = @id", new { id });
    public IReadOnlyList<Item> List() => _conn.Query<Item>("SELECT id AS Id, name AS Name, size AS Size FROM items ORDER BY id").AsList();

    public void InsertThenRollback(Item i)
    {
        using var tx = _conn.BeginTransaction();
        _conn.Execute("INSERT INTO items (id, name, size) VALUES (@Id, @Name, @Size)", i, tx);
        tx.Rollback();
    }

    public void InsertBothCommitted(Item a, Item b)
    {
        using var tx = _conn.BeginTransaction();
        _conn.Execute("INSERT INTO items (id, name, size) VALUES (@Id, @Name, @Size)", a, tx);
        _conn.Execute("INSERT INTO items (id, name, size) VALUES (@Id, @Name, @Size)", b, tx);
        tx.Commit();
    }

    public void Dispose() => _conn.Dispose();
}
