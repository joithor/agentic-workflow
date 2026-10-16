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

    // The rows, then the table column behind each result column (the server's RowDescription gives a table OID and attribute number; pg_attribute names it).
    // A computed column has none. Runs inside the same read-only transaction, once the result reader is closed.
    private async ValueTask<Result<Rows>> ReadAsync(string sql, int rowLimit, CancellationToken cancellationToken)
    {
        var (rows, origins) = await ReadRowsAsync(sql, rowLimit, cancellationToken).ConfigureAwait(false);
        return rows with { SourceColumns = await NamesAsync(origins, cancellationToken).ConfigureAwait(false) };
    }

    private async ValueTask<(Rows Rows, List<(uint Table, short Number)?> Origins)> ReadRowsAsync(string sql, int rowLimit, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(sql, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                var rows = new List<IReadOnlyList<object?>>();
                var truncated = false;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (rows.Count == rowLimit)
                    {
                        truncated = true;
                        break;
                    }

                    rows.Add([.. Enumerable.Range(0, columns.Count).Select(i => reader.IsDBNull(i) ? null : reader.GetValue(i))]);
                }

                return (new Rows(columns, rows, truncated), Origins(reader));
            }
        }
    }

    private static List<(uint Table, short Number)?> Origins(NpgsqlDataReader reader)
    {
        try
        {
            return [.. reader.GetColumnSchema().Select(static column => column is { TableOID: > 0, ColumnAttributeNumber: { } number } ? ((uint, short)?)(column.TableOID, number) : null)];
        }
        catch (NotSupportedException)
        {
            return [.. Enumerable.Repeat<(uint, short)?>(element: null, count: reader.FieldCount)];
        }
    }

    private async ValueTask<List<string?>> NamesAsync(List<(uint Table, short Number)?> origins, CancellationToken cancellationToken)
    {
        var names = new List<string?>();
        foreach (var origin in origins)
        {
            names.Add(origin is { } o ? await NameAsync(o, cancellationToken).ConfigureAwait(false) : null);
        }

        return names;
    }

    private async ValueTask<string?> NameAsync((uint Table, short Number) origin, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand("SELECT attname::text FROM pg_attribute WHERE attrelid = @table AND attnum = @number", connection);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("table", NpgsqlTypes.NpgsqlDbType.Oid, origin.Table);
            _ = command.Parameters.AddWithValue("number", NpgsqlTypes.NpgsqlDbType.Smallint, origin.Number);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }
    }
}
