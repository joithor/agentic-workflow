using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// The checks <see cref="MigrationRunner"/> runs before it writes anything: the migration set is 1..n in order, the
/// file is this runner's (or new), and the applied history matches the build (spec §4b.2).
/// </summary>
internal static class MigrationChecks
{
    internal static Result<Unit> CheckSequence(IReadOnlyList<Migration> migrations)
    {
        if (migrations.Count == 0)
        {
            return DataErrors.MigrationSequence.With("The migration set is empty; a package with no schema doesn't call the runner.");
        }

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

    // True when the file has this runner's schema_version (exact columns, in order); false for a new, empty file.
    internal static async Task<Result<bool>> CheckOwnership(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = MigrationRunner.SelectOwnership;
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        var (columns, hasTables) = (reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetInt64(1) == 1);
        if (columns is null)
        {
            return hasTables
                ? DataErrors.ForeignDatabase.With(
                    $"{connection.DataSource} has tables but no schema_version table, so this runner didn't create it. Nothing was written. Check the path points at this package's own database.")
                : false;
        }

        return string.Equals(columns, MigrationRunner.VersionColumns, StringComparison.Ordinal)
            ? true
            : DataErrors.ForeignDatabase.With(
                $"{connection.DataSource} has a schema_version table with columns ({columns}), not this runner's ({MigrationRunner.VersionColumns}), so this runner didn't create it. Nothing was written. Check the path points at this package's own database.");
    }

    internal static async Task<Result<int>> CheckApplied(SqliteConnection connection, IReadOnlyList<Migration> migrations, CancellationToken cancellationToken)
    {
        var command = connection.CreateCommand();
        await using var commandScope = command.ConfigureAwait(false);
        command.CommandText = MigrationRunner.SelectApplied;
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerScope = reader.ConfigureAwait(false);
        var expected = 1;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var (version, name, checksum) = (reader.GetInt64(0), reader.GetString(1), reader.GetString(2));
            var problem = Problem(migrations, expected, version, name, checksum);
            if (problem is not null)
            {
                return problem;
            }

            expected++;
        }

        return expected - 1;
    }

    private static Error? Problem(IReadOnlyList<Migration> migrations, int expected, long version, string name, string checksum)
    {
        var v = version.ToString(CultureInfo.InvariantCulture);
        if (version > migrations.Count)
        {
            return DataErrors.MigrationUnknown.With(
                $"The database has migration {v} ({name}); this build knows {migrations.Count.ToString(CultureInfo.InvariantCulture)}. Use a newer build or restore a backup.");
        }

        if (version != expected)
        {
            return DataErrors.MigrationDrift.With(
                $"schema_version has version {v} where {expected.ToString(CultureInfo.InvariantCulture)} was expected: the applied history must be exactly 1, 2, …, n. Restore a backup.");
        }

        var known = migrations[expected - 1];
        return string.Equals(known.Name, name, StringComparison.Ordinal) && string.Equals(known.Checksum, checksum, StringComparison.Ordinal)
            ? null
            : DataErrors.MigrationDrift.With(
                $"Migration {v} was applied as '{name}' with different text or name than this build's '{known.Name}'. Applied migrations never change: add a new migration instead.");
    }
}
