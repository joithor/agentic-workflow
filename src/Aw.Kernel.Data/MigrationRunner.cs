using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// Brings a database's schema up to date from numbered migrations (spec §4b.2). It checks the set before
/// touching the database, refuses drift in applied migrations and databases newer than the build, and applies
/// each pending migration in its own transaction together with its 'schema_version' row. A migration whose
/// SQL fails is a bug: it rolls back and the exception propagates.
/// </summary>
/// <param name="connections">Opens the database.</param>
/// <param name="clock">Stamps 'applied_at' (UTC, millisecond precision, like Node's toISOString).</param>
public sealed class MigrationRunner(ISqliteConnectionFactory connections, TimeProvider clock)
{
    internal const string CreateVersionTable =
        "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER PRIMARY KEY, name TEXT NOT NULL, checksum TEXT NOT NULL, applied_at TEXT NOT NULL)";

    internal const string SelectApplied = "SELECT version, name, checksum FROM schema_version ORDER BY version";

    internal const string SelectCurrent = "SELECT COALESCE(MAX(version), 0) FROM schema_version";

    internal const string InsertApplied = "INSERT INTO schema_version (version, name, checksum, applied_at) VALUES ($v, $n, $c, $t)";

    /// <summary>Applies every pending migration, or those up to <paramref name="targetVersion"/>.</summary>
    /// <param name="migrations">The full migration set, in version order.</param>
    /// <param name="cancellationToken">Cancels the run between migrations.</param>
    /// <param name="targetVersion">Stop after this version; null applies all. Lets a test check each step on the previous schema.</param>
    /// <returns>What was applied, or AW-DATA-002/003/005/006/007.</returns>
    public async ValueTask<Result<MigrationReport>> Migrate(IReadOnlyList<Migration> migrations, CancellationToken cancellationToken, int? targetVersion = null)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        var sequence = CheckSequence(migrations);
        if (sequence.IsError)
        {
            return sequence.Error;
        }

        var opened = await connections.Open(cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened.Error;
        }

        var connection = opened.Value;

        await using var connectionScope = connection.ConfigureAwait(false);
        try
        {
            await Run(connection, CreateVersionTable, cancellationToken).ConfigureAwait(false);
            var applied = await CheckApplied(connection, migrations, cancellationToken).ConfigureAwait(false);
            return applied.IsError
                ? applied.Error
                : await ApplyPending(connection, migrations, applied.Value, targetVersion ?? int.MaxValue, cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, "Reading schema_version", out var error))
        {
            return error;
        }
    }

    private static Result<Unit> CheckSequence(IReadOnlyList<Migration> migrations)
    {
        for (var i = 0; i < migrations.Count; i++)
        {
            if (migrations[i].Version != i + 1)
            {
                return DataErrors.MigrationSequence.With(
                    $"Migration {(i + 1).ToString(CultureInfo.InvariantCulture)} of the set has version {migrations[i].Version.ToString(CultureInfo.InvariantCulture)}; versions must run 1, 2, 3, … with no gaps, duplicates or reordering.");
            }
        }

        return Results.Ok();
    }

    private static async Task<Result<int>> CheckApplied(SqliteConnection connection, IReadOnlyList<Migration> migrations, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = SelectApplied;
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        var current = 0;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var version = reader.GetInt32(0);
            if (version > migrations.Count)
            {
                return DataErrors.MigrationUnknown.With(
                    $"The database has migration {version.ToString(CultureInfo.InvariantCulture)} ({reader.GetString(1)}); this build knows {migrations.Count.ToString(CultureInfo.InvariantCulture)}. Use a newer build or restore a backup.");
            }

            var known = migrations[version - 1];
            if (!string.Equals(known.Checksum, reader.GetString(2), StringComparison.Ordinal))
            {
                return DataErrors.MigrationDrift.With(
                    $"Migration {version.ToString(CultureInfo.InvariantCulture)} ({known.Name}) was applied with different text. Applied migrations never change: add a new migration instead.");
            }

            current = version;
        }

        return current;
    }

    private async Task<Result<MigrationReport>> ApplyPending(SqliteConnection connection, IReadOnlyList<Migration> migrations, int from, int target, CancellationToken cancellationToken)
    {
        var applied = new List<int>();
        foreach (var migration in migrations.Where(m => m.Version > from && m.Version <= target))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = await ApplyOne(connection, migration, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome.Error;
            }

            if (outcome.Value)
            {
                applied.Add(migration.Version);
            }
        }

        return new MigrationReport(from, applied.Count == 0 ? from : applied[^1], applied);
    }

    private async Task<Result<bool>> ApplyOne(SqliteConnection connection, Migration migration, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await using var transactionScope = transaction.ConfigureAwait(false);
            // Another runner may have applied it since we read schema_version; re-check inside the write lock.
            if (await Current(connection, transaction, cancellationToken).ConfigureAwait(false) >= migration.Version)
            {
                return false;
            }

            await Run(connection, migration.Sql, cancellationToken, transaction).ConfigureAwait(false);
            await Record(connection, transaction, migration, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SqliteException exception) when (SqliteErrors.TryMap(exception, $"Applying migration {migration.Version.ToString(CultureInfo.InvariantCulture)} ({migration.Name})", out var error))
        {
            return error;
        }
    }

    private async Task Record(SqliteConnection connection, SqliteTransaction transaction, Migration migration, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = InsertApplied;
        _ = command.Bind("$v", migration.Version).Bind("$n", migration.Name).Bind("$c", migration.Checksum)
            .Bind("$t", clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> Current(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = SelectCurrent;
        return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!; // why: MAX with COALESCE always returns one integer
    }

    private static async Task Run(SqliteConnection connection, string sql, CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.Transaction = transaction;
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
