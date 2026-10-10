// Code under test for the R7 spike, linked into both test projects.
using Microsoft.Data.Sqlite;

namespace Spike.R7;

public static class Slug
{
    public static string Normalize(string input)
    {
        var chars = new System.Text.StringBuilder(input.Length);
        var dash = false;
        foreach (var c in input.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') { chars.Append(c); dash = false; }
            else if (!dash && chars.Length > 0) { chars.Append('-'); dash = true; }
        }
        return chars.ToString().TrimEnd('-');
    }
}

public static class Budget
{
    public static int Remaining(int total, int used) =>
        total < 0 || used < 0 ? throw new ArgumentOutOfRangeException(nameof(total)) : Math.Max(0, total - used);
}

public sealed class NoteStore(string path) : IDisposable
{
    readonly SqliteConnection _conn = Open(path);

    static SqliteConnection Open(string path)
    {
        var c = new SqliteConnection($"Data Source={path}");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode = wal; CREATE TABLE IF NOT EXISTS notes (id TEXT PRIMARY KEY, body TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
        return c;
    }

    public void Put(string id, string body)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO notes (id, body) VALUES ($id, $body)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$body", body);
        cmd.ExecuteNonQuery();
    }

    public string? Get(string id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT body FROM notes WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    public void Dispose() => _conn.Dispose();
}
