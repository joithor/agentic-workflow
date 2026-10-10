using Aw.Kernel.Persistence;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// One SQLite write transaction (BEGIN IMMEDIATE) on its own connection. Repositories write through it as an
/// <see cref="ISqliteSession"/>; <see cref="Commit"/> commits once; disposing without a commit rolls back.
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork, ISqliteSession
{
    private bool _committed;

    private SqliteUnitOfWork(SqliteConnection connection, SqliteTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    /// <inheritdoc />
    public SqliteConnection Connection { get; }

    /// <inheritdoc />
    public SqliteTransaction Transaction { get; }

    /// <inheritdoc />
    public async ValueTask<Result<Unit>> Commit(CancellationToken cancellationToken)
    {
        if (_committed)
        {
            throw new InvalidOperationException("This unit of work is already committed; begin a new one.");
        }

        try
        {
            await Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _committed = true;
            return Results.Ok();
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, "Committing", out var error))
        {
            return error;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => SqliteTransactions.Close(Connection, Transaction);

    internal static async ValueTask<Result<SqliteUnitOfWork>> Begin(ISqliteConnectionFactory connections, CancellationToken cancellationToken) =>
        // BEGIN IMMEDIATE: take the write lock now, so a busy database fails here, not mid-use-case.
        (await SqliteTransactions.Begin(await connections.Open(cancellationToken).ConfigureAwait(false), deferred: false, cancellationToken).ConfigureAwait(false))
            .Map(opened => new SqliteUnitOfWork(opened.Connection, opened.Transaction));
}
