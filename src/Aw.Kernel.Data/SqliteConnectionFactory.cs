using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// Opens connections so every package reads and writes its SQLite files the same way (spec §4b.2). Read-write
/// connections get WAL (checked, not assumed), foreign keys, synchronous=NORMAL and the busy timeout; read-only
/// connections never create the file and refuse writes. Pooling is off: CLIs open once per run, and pooled handles
/// would keep the file open after the caller is done with it.
/// </summary>
/// <param name="options">The database file and busy timeout.</param>
public sealed class SqliteConnectionFactory(SqliteOptions options) : ISqliteConnectionFactory
{
    /// <inheritdoc />
    public ValueTask<Result<SqliteConnection>> Open(CancellationToken cancellationToken) =>
        OpenWith(SqliteOpenMode.ReadWriteCreate, ReadWritePragmas, cancellationToken);

    /// <inheritdoc />
    public ValueTask<Result<SqliteConnection>> OpenReadOnly(CancellationToken cancellationToken) =>
        OpenWith(SqliteOpenMode.ReadOnly, ReadOnlyPragmas, cancellationToken);

    private async ValueTask<Result<SqliteConnection>> OpenWith(
        SqliteOpenMode mode,
        Func<SqliteConnection, CancellationToken, Task<Result<Unit>>> pragmas,
        CancellationToken cancellationToken)
    {
        var runtime = SqliteRuntime.Check();
        if (runtime.IsError)
        {
            return runtime.Error;
        }

        var connection = new SqliteConnection(ConnectionString(mode));
        var handedOver = false;
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var set = await pragmas(connection, cancellationToken).ConfigureAwait(false);
            if (set.IsError)
            {
                return set.Error;
            }

            handedOver = true;
            return connection;
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, $"Opening {options.DatabasePath}", out var error))
        {
            return error;
        }
        finally
        {
            // Every path that doesn't hand the connection to the caller closes it, including cancellation and bugs.
            if (!handedOver)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private string ConnectionString(SqliteOpenMode mode) => new SqliteConnectionStringBuilder
    {
        DataSource = options.DatabasePath,
        Mode = mode,
        Pooling = false,
        // Microsoft.Data.Sqlite retries SQLITE_BUSY until the command timeout; keep it in step with busy_timeout.
        DefaultTimeout = (int)Math.Ceiling(options.BusyTimeout.TotalSeconds),
    }.ToString();

    private readonly string _busyMilliseconds = ((long)options.BusyTimeout.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

    private async Task<Result<Unit>> ReadWritePragmas(SqliteConnection connection, CancellationToken cancellationToken)
    {
        _ = await Scalar(connection, $"PRAGMA busy_timeout = {_busyMilliseconds}", cancellationToken).ConfigureAwait(false);
        // journal_mode returns the mode it ended up in: a filesystem without shared memory stays in 'delete', and
        // everything that relies on WAL (readers beside a writer, synchronous=NORMAL being crash-safe) stops holding.
        var journal = Convert.ToString(await Scalar(connection, "PRAGMA journal_mode = WAL", cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (!string.Equals(journal, "wal", StringComparison.OrdinalIgnoreCase))
        {
            return DataErrors.Unavailable.With(
                $"Opening {options.DatabasePath}: SQLite can't use WAL here (journal_mode is '{journal}'). Use a local disk.");
        }

        _ = await Scalar(connection, "PRAGMA foreign_keys = ON; PRAGMA synchronous = NORMAL", cancellationToken).ConfigureAwait(false);
        return Results.Ok();
    }

    private async Task<Result<Unit>> ReadOnlyPragmas(SqliteConnection connection, CancellationToken cancellationToken)
    {
        _ = await Scalar(connection, $"PRAGMA busy_timeout = {_busyMilliseconds}; PRAGMA query_only = ON", cancellationToken).ConfigureAwait(false);
        return Results.Ok();
    }

    private static async Task<object?> Scalar(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }
}
