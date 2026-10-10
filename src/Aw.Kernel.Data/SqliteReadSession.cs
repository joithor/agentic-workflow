using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// A read-only session (query_only, a deferred transaction): every query in it reads one consistent snapshot, it
/// never waits for writers in WAL mode, it never creates the file, and writes through it fail with AW-DATA-002.
/// For status, doctor and report commands.
/// </summary>
public sealed class SqliteReadSession : ISqliteSession, IAsyncDisposable
{
    private SqliteReadSession(SqliteConnection connection, SqliteTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    /// <inheritdoc />
    public SqliteConnection Connection { get; }

    /// <inheritdoc />
    public SqliteTransaction? Transaction { get; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => SqliteTransactions.Close(Connection, Transaction!);

    internal static async ValueTask<Result<SqliteReadSession>> Begin(ISqliteConnectionFactory connections, CancellationToken cancellationToken) =>
        (await SqliteTransactions.Begin(await connections.OpenReadOnly(cancellationToken).ConfigureAwait(false), deferred: true, cancellationToken).ConfigureAwait(false))
            .Map(opened => new SqliteReadSession(opened.Connection, opened.Transaction));
}
