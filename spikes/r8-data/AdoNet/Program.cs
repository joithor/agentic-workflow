// R8 variant (a): hand-written ADO.NET over Microsoft.Data.Sqlite with an IRowMapper<T> per row type.
using Microsoft.Data.Sqlite;
using Spike.R8;

return Scenario.Run("adonet", args.Length > 0 ? args[0] : "adonet.db", path => new AdoItemStore(path));

interface IRowMapper<out T> { T Map(SqliteDataReader r); }

sealed class ItemMapper : IRowMapper<Item>
{
    public static readonly ItemMapper Instance = new();
    public Item Map(SqliteDataReader r) => new(r.GetString(0), r.GetString(1), r.GetInt32(2));
}

sealed class AdoItemStore : IItemStore
{
    const string CreateSql = "CREATE TABLE IF NOT EXISTS items (id TEXT PRIMARY KEY, name TEXT NOT NULL, size INTEGER NOT NULL)";
    const string InsertSql = "INSERT INTO items (id, name, size) VALUES ($id, $name, $size)";
    const string UpdateSql = "UPDATE items SET name = $name, size = $size WHERE id = $id";
    const string DeleteSql = "DELETE FROM items WHERE id = $id";
    const string GetSql = "SELECT id, name, size FROM items WHERE id = $id";
    const string ListSql = "SELECT id, name, size FROM items ORDER BY id";

    readonly SqliteConnection _conn;

    public AdoItemStore(string path)
    {
        _conn = new SqliteConnection($"Data Source={path}");
        _conn.Open();
        Exec("PRAGMA journal_mode = wal");
    }

    public string JournalMode() => (string)Scalar("PRAGMA journal_mode")!;
    public void CreateSchema() => Exec(CreateSql);
    public void Insert(Item i) => Write(InsertSql, i, null);
    public void Update(Item i) => Write(UpdateSql, i, null);

    public void Delete(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = DeleteSql;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public Item? Get(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = GetSql;
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ItemMapper.Instance.Map(r) : null;
    }

    public IReadOnlyList<Item> List()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = ListSql;
        using var r = cmd.ExecuteReader();
        var list = new List<Item>();
        while (r.Read()) list.Add(ItemMapper.Instance.Map(r));
        return list;
    }

    public void InsertThenRollback(Item i)
    {
        using var tx = _conn.BeginTransaction();
        Write(InsertSql, i, tx);
        tx.Rollback();
    }

    public void InsertBothCommitted(Item a, Item b)
    {
        using var tx = _conn.BeginTransaction();
        Write(InsertSql, a, tx);
        Write(InsertSql, b, tx);
        tx.Commit();
    }

    void Write(string sql, Item i, SqliteTransaction? tx)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("$id", i.Id);
        cmd.Parameters.AddWithValue("$name", i.Name);
        cmd.Parameters.AddWithValue("$size", i.Size);
        cmd.ExecuteNonQuery();
    }

    void Exec(string sql) { using var c = _conn.CreateCommand(); c.CommandText = sql; c.ExecuteNonQuery(); }
    object? Scalar(string sql) { using var c = _conn.CreateCommand(); c.CommandText = sql; return c.ExecuteScalar(); }
    public void Dispose() => _conn.Dispose();
}
