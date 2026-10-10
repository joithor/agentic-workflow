using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// Opens connections with WAL, foreign keys, synchronous=NORMAL and the busy timeout set, so every package
/// reads and writes its SQLite files the same way (spec §4b.2). Pooling is off: CLIs open once per run, and
/// pooled handles would keep the file open after the caller is done with it.
/// </summary>
/// <param name="options">The database file and busy timeout.</param>
public sealed class SqliteConnectionFactory(SqliteOptions options) : ISqliteConnectionFactory
{
    /// <inheritdoc />
    public async ValueTask<Result<SqliteConnection>> Open(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString());
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await SetPragmas(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, $"Opening {options.DatabasePath}", out var error))
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return error;
        }
    }

    private string ConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = options.DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = false,
        // Microsoft.Data.Sqlite retries SQLITE_BUSY until the command timeout; keep it in step with busy_timeout.
        DefaultTimeout = (int)Math.Ceiling(options.BusyTimeout.TotalSeconds),
    }.ToString();

    private async Task SetPragmas(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var busyMs = ((long)options.BusyTimeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText =
            $"PRAGMA busy_timeout = {busyMs}; PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON; PRAGMA synchronous = NORMAL;";
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
