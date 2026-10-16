using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>
/// Reads a SQLite file through a copy of it. The source is opened Mode=ReadOnly only to take an online backup into the work
/// directory; the session then queries the copy opened read-only with query_only (Aw.Kernel.Data's OpenReadOnly), so the
/// evidence connection never holds a write handle on the source.
/// </summary>
internal sealed class SqliteDatabaseReader : IDatabaseReader
{
    internal const string CopyName = "evidence-copy.sqlite";

    private const int NoAttachedDatabases = 7;   // SQLITE_LIMIT_ATTACHED

    /// <inheritdoc/>
    public async ValueTask<Result<IReadSession>> OpenAsync(DbTarget target, CancellationToken cancellationToken)
    {
        if (target is not SqliteTarget sqlite)
        {
            return EvidenceErrors.DatabaseRefused.With("this reader opens SQLite files only");
        }

        if (!File.Exists(sqlite.SourcePath))
        {
            return EvidenceErrors.DatabaseUnavailable.With($"{sqlite.SourcePath} is not a file");
        }

        var copy = Path.Combine(sqlite.WorkDirectory, CopyName);
        var copied = await CopyAsync(sqlite, copy, cancellationToken).ConfigureAwait(false);
        if (copied.IsError)
        {
            return copied.Error;
        }

        var seeded = await SeedAsync(sqlite.SeedSql, copy, cancellationToken).ConfigureAwait(false);
        if (seeded.IsError)
        {
            return seeded.Error;
        }

        var opened = await new SqliteConnectionFactory(new SqliteOptions(copy)).OpenReadOnly(cancellationToken).ConfigureAwait(false);
        return opened.IsError ? EvidenceErrors.DatabaseUnavailable.With($"can't open the copy {copy}: {opened.Error.Message}") : new SqliteReadSession(opened.Value);
    }

    // The seed runs on its own read-write connection to the copy, closed before the read-only one opens; the source is never touched. The statement guard
    // refuses ATTACH, DETACH, VACUUM and load_extension, and the connection allows no attached database at all (SQLITE_LIMIT_ATTACHED = 0), so no other file is reachable.
    private static async ValueTask<Result<Unit>> SeedAsync(string? seedSql, string copy, CancellationToken cancellationToken)
    {
        if (seedSql is null)
        {
            return Unit.Value;
        }

        var allowed = SeedStatements.Check(seedSql);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        var opened = await OpenSeedConnectionAsync(copy, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return EvidenceErrors.DatabaseUnavailable.With($"can't open the copy {copy} to seed it: {opened.Error.Message}");
        }

        await using (opened.Value.ConfigureAwait(false))
        {
            try
            {
                var command = opened.Value.CreateCommand();
                await using (command.ConfigureAwait(false))
                {
                    command.CommandText = seedSql;
                    _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (SqliteException exception)
            {
                return EvidenceErrors.DbScriptInvalid.With($"the seed failed on the copy: {exception.Message}");
            }
        }

        return Unit.Value;
    }

    // Read-write on the copy with no attached database allowed (SQLITE_LIMIT_ATTACHED = 0).
    internal static async ValueTask<Result<Microsoft.Data.Sqlite.SqliteConnection>> OpenSeedConnectionAsync(string copy, CancellationToken cancellationToken)
    {
        var opened = await new SqliteConnectionFactory(new SqliteOptions(copy)).Open(cancellationToken).ConfigureAwait(false);
        if (opened.IsSuccess)
        {
            _ = SQLitePCL.raw.sqlite3_limit(opened.Value.Handle, NoAttachedDatabases, 0);
        }

        return opened;
    }

    private static async ValueTask<Result<Unit>> CopyAsync(SqliteTarget target, string copy, CancellationToken cancellationToken)
    {
        _ = Directory.CreateDirectory(target.WorkDirectory);
        foreach (var leftover in new[] { copy, copy + "-wal", copy + "-shm" })
        {
            File.Delete(leftover);
        }

        var source = await new SqliteConnectionFactory(new SqliteOptions(target.SourcePath)).OpenReadOnly(cancellationToken).ConfigureAwait(false);
        if (source.IsError)
        {
            return EvidenceErrors.DatabaseUnavailable.With($"can't open {target.SourcePath}: {source.Error.Message}");
        }

        await using (source.Value.ConfigureAwait(false))
        {
            var destination = await new SqliteConnectionFactory(new SqliteOptions(copy)).Open(cancellationToken).ConfigureAwait(false);
            if (destination.IsError)
            {
                return EvidenceErrors.DatabaseUnavailable.With($"can't make the copy {copy}: {destination.Error.Message}");
            }

            await using (destination.Value.ConfigureAwait(false))
            {
                try
                {
                    source.Value.BackupDatabase(destination.Value);
                }
                catch (SqliteException exception)
                {
                    return EvidenceErrors.DatabaseUnavailable.With($"can't copy {target.SourcePath}: {exception.Message}");
                }
            }
        }

        return Unit.Value;
    }
}
