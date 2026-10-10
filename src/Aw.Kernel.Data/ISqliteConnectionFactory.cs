using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Opens connections to one database file.</summary>
public interface ISqliteConnectionFactory
{
    /// <summary>Opens a connection with the standard pragmas set.</summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The open connection (the caller disposes it), or AW-DATA-002 / AW-DATA-003.</returns>
    public ValueTask<Result<SqliteConnection>> Open(CancellationToken cancellationToken);
}
