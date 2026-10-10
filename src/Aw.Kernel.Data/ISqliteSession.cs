using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>An open connection and the transaction commands run in, if any. Repositories read and write through it.</summary>
public interface ISqliteSession
{
    /// <summary>The open connection.</summary>
    public SqliteConnection Connection { get; }

    /// <summary>The transaction every command joins, or null outside one.</summary>
    public SqliteTransaction? Transaction { get; }
}
