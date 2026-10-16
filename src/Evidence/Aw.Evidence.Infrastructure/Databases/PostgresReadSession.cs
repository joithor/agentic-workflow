using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;
using Npgsql;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>
/// A connection that runs one statement at a time, each in its own BEGIN READ ONLY transaction that is always rolled back. A
/// second statement is refused here and the server refuses it again (one statement per extended-protocol command). A write the server
/// refuses (read-only transaction, insufficient privilege) is a refusal; any other failure is a failed query.
/// </summary>
internal sealed class PostgresReadSession(NpgsqlConnection connection) : IReadSession
{
    // active_sql_transaction (switching to read-write), read_only_sql_transaction and insufficient_privilege.
    private static readonly string[] _refusingStates = ["25001", "25006", "42501"];

    /// <inheritdoc/>
    public async ValueTask<Result<Rows>> QueryAsync(string sql, int rowLimit, CancellationToken cancellationToken)
    {
        if (SqlText.Statements(sql).Count != 1)
        {
            return EvidenceErrors.DatabaseRefused.With("exactly one statement runs at a time");
        }

        try
        {
            await RunAsync("BEGIN READ ONLY", cancellationToken).ConfigureAwait(false);
            try
            {
                // Postgres lets the first statement of a transaction switch it to read-write (SET transaction_read_only = off); after any query it refuses.
                await RunAsync("SELECT 1", cancellationToken).ConfigureAwait(false);
                return await ReadAsync(sql, rowLimit, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await RollbackAsync().ConfigureAwait(false);
            }
        }
        catch (PostgresException exception) when (IsRefusal(exception))
        {
            return EvidenceErrors.DatabaseRefused.With($"the server refused a write (SQLSTATE {exception.SqlState}): {exception.MessageText}");
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException)
        {
            return EvidenceErrors.RunFailed.With($"the query failed: {(exception is PostgresException server ? server.SqlState + " " + server.MessageText : exception.Message)}");
        }
    }

    // A second command in one prepared statement is a syntax error to the server; any other syntax error is the script's own mistake.
    private static bool IsRefusal(PostgresException exception) =>
        _refusingStates.Contains(exception.SqlState, StringComparer.Ordinal)
        || (string.Equals(exception.SqlState, "42601", StringComparison.Ordinal) && exception.MessageText.Contains("multiple commands", StringComparison.Ordinal));

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private async ValueTask RunAsync(string sql, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(sql, connection);
        await using (command.ConfigureAwait(false))
        {
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // A failed statement leaves the transaction aborted; rolling back always returns the session to a usable state.
    private async ValueTask RollbackAsync()
    {
        try
        {
            await RunAsync("ROLLBACK", CancellationToken.None).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            // The connection is gone; the next statement reports that itself.
        }
    }

    private async ValueTask<Result<Rows>> ReadAsync(string sql, int rowLimit, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(sql, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                var rows = new List<IReadOnlyList<object?>>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (rows.Count == rowLimit)
                    {
                        return new Rows(columns, rows, Truncated: true);
                    }

                    rows.Add([.. Enumerable.Range(0, columns.Count).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i))]);
                }

                return new Rows(columns, rows, Truncated: false);
            }
        }
    }
}
