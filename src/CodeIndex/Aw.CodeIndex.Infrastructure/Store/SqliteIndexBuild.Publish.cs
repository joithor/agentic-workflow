using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexBuild
{
    private const string WriteMeta = "INSERT OR REPLACE INTO meta(key, value) VALUES ($key, $value)";

    public ValueTask<Result<IndexHeader>> Publish(IndexMeta meta, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(meta);
        return SqliteFailure.Guard("Publishing the index", async () => await PublishFile(meta, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<IndexHeader>> PublishFile(IndexMeta meta, CancellationToken cancellationToken)
    {
        var stored = meta with { Counts = await CountRows(cancellationToken).ConfigureAwait(false), Schema = CodeIndexRules.SchemaVersion };
        await WriteMetaRows(stored, cancellationToken).ConfigureAwait(false);
        var header = await HeaderReader.Read(connection, cancellationToken).ConfigureAwait(false);
        var sealedFile = await Seal(cancellationToken).ConfigureAwait(false);
        if (sealedFile.IsError)
        {
            return sealedFile.Error;
        }

        // The swap is the only step a reader can see; a cancel before it leaves the live index alone.
        cancellationToken.ThrowIfCancellationRequested();
        await target.Files.SwapPointer(target.Repo, target.Ulid, cancellationToken).ConfigureAwait(false);
        _published = true;
        target.Files.ClearBuild(target.Repo, target.Ulid);
        return header;
    }

    private async Task<IndexCounts> CountRows(CancellationToken cancellationToken)
        => new(
            (int)await Sql.Count(connection, "SELECT COUNT(*) FROM files", cancellationToken).ConfigureAwait(false),
            (int)await Sql.Count(connection, "SELECT COUNT(*) FROM symbols", cancellationToken).ConfigureAwait(false),
            (int)await Sql.Count(connection, "SELECT COUNT(*) FROM edges", cancellationToken).ConfigureAwait(false));

    // Checks the file, leaves WAL mode and closes it, so the published file is immutable and has no sidecars.
    private async Task<Result<Unit>> Seal(CancellationToken cancellationToken)
    {
        if (!string.Equals(await Sql.Scalar(connection, "PRAGMA quick_check(1)", cancellationToken).ConfigureAwait(false) as string, "ok", StringComparison.Ordinal))
        {
            return CodeIndexErrors.IndexDamaged.With("The build file failed its integrity check; nothing was published.");
        }

        _ = await Sql.Scalar(connection, "PRAGMA wal_checkpoint(TRUNCATE)", cancellationToken).ConfigureAwait(false);
        if (!string.Equals(await Sql.Scalar(connection, "PRAGMA journal_mode = DELETE", cancellationToken).ConfigureAwait(false) as string, "delete", StringComparison.OrdinalIgnoreCase))
        {
            return CodeIndexErrors.IndexDamaged.With("The build file could not leave WAL mode; nothing was published.");
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        _closed = true;
        IndexFiles.DeleteSidecars(target.Files.DbPath(target.Repo, target.Ulid));
        return Unit.Value;
    }

    private async Task WriteMetaRows(IndexMeta meta, CancellationToken cancellationToken)
    {
        var rows = new List<(string Key, string Value)>
        {
            ("commit", meta.Commit.Value),
            ("default_branch", meta.DefaultBranch),
            ("built_at", meta.BuiltAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
            ("schema", meta.Schema.ToString(CultureInfo.InvariantCulture)),
            ("parser_id", meta.ParserVersion),
            ("files", meta.Counts.Files.ToString(CultureInfo.InvariantCulture)),
            ("symbols", meta.Counts.Symbols.ToString(CultureInfo.InvariantCulture)),
            ("edges", meta.Counts.Edges.ToString(CultureInfo.InvariantCulture)),
        };
        if (meta.LockSha is { } lockSha)
        {
            rows.Add(("lock_sha", lockSha));
        }

        _ = await Sql.Run(connection, "DELETE FROM meta", cancellationToken).ConfigureAwait(false);
        foreach (var (key, value) in rows)
        {
            var command = Sql.Command(connection, WriteMeta).With("$key", key).With("$value", value);
            await using (command.ConfigureAwait(false))
            {
                _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
