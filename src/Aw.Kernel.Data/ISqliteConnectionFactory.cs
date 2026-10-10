using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Opens connections to one database file.</summary>
public interface ISqliteConnectionFactory
{
    /// <summary>Opens a read-write connection with the standard pragmas, creating the file if it doesn't exist.</summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open connection (the caller disposes it), or AW-DATA-002 / 003 / 008.</returns>
    public ValueTask<Result<SqliteConnection>> Open(CancellationToken cancellationToken);

    /// <summary>
    /// Opens a read-only connection (query_only). It never creates the file: a missing file is AW-DATA-002.
    /// It doesn't switch the journal mode, so it leaves a database it only reads byte-for-byte unchanged.
    /// </summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open connection (the caller disposes it), or AW-DATA-002 / 003 / 008.</returns>
    public ValueTask<Result<SqliteConnection>> OpenReadOnly(CancellationToken cancellationToken);
}
