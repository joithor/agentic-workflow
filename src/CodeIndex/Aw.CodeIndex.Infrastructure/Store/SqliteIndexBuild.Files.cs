using Aw.CodeIndex.Core.Model;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexBuild
{
    // Edges into the symbols of the given files become unresolved; one that would then repeat an unresolved edge is dropped, since the unique index
    // would refuse the second, and every syntax edge is resolved again after the structure write anyway.
    private const string Unresolve = "UPDATE OR IGNORE edges SET dst = NULL WHERE dst IN (SELECT id FROM symbols WHERE file = $path)";

    private const string DropRepeats = "DELETE FROM edges WHERE dst IN (SELECT id FROM symbols WHERE file = $path)";

    private const string DeleteFile = "DELETE FROM files WHERE path = $path";

    private static readonly string[] _detachSteps = [Unresolve, DropRepeats, DeleteFile];

    public ValueTask<Result<Unit>> WriteFiles(IReadOnlyList<FileRecord> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        return SqliteFailure.Guard("Writing files", async () => await Write(files, cancellationToken).ConfigureAwait(false));
    }

    public ValueTask<Result<Unit>> RemoveFiles(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return SqliteFailure.Guard("Removing files", async () => await Remove(paths, cancellationToken).ConfigureAwait(false));
    }

    private async Task<Result<Unit>> Write(IReadOnlyList<FileRecord> files, CancellationToken cancellationToken)
    {
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await Detach(files.Select(f => f.Path), transaction, cancellationToken).ConfigureAwait(false);
            var rows = new RowWriter(connection, transaction);
            await using (rows.ConfigureAwait(false))
            {
                foreach (var file in files)
                {
                    await rows.Write(file, cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }

    private async Task<Result<Unit>> Remove(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await Detach(paths, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }

    /// <summary>Takes the files' rows out: their symbols, call sites, bands and bodies cascade away, and the edges into them become unresolved.</summary>
    private async Task Detach(IEnumerable<string> paths, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        foreach (var path in paths)
        {
            foreach (var sql in _detachSteps)
            {
                var command = Sql.Command(connection, sql, transaction).With("$path", path);
                await using (command.ConfigureAwait(false))
                {
                    _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
