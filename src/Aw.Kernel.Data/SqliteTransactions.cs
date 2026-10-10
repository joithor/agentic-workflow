using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Begins and closes the transaction a session owns, closing the connection on every path that fails.</summary>
internal static class SqliteTransactions
{
    internal static async ValueTask<Result<(SqliteConnection Connection, SqliteTransaction Transaction)>> Begin(
        Result<SqliteConnection> opened,
        bool deferred,
        CancellationToken cancellationToken)
    {
        if (opened.IsError)
        {
            return opened.Error;
        }

        var connection = opened.Value;
        var handedOver = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var transaction = connection.BeginTransaction(deferred);
            handedOver = true;
            return (connection, transaction);
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, "Beginning a transaction", out var error))
        {
            return error;
        }
        finally
        {
            if (!handedOver)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    internal static async ValueTask Close(SqliteConnection connection, SqliteTransaction transaction)
    {
        try
        {
            // Disposing an uncommitted SqliteTransaction rolls it back.
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
