using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>
/// One build file in progress (spec §4.1, §5.7): the only way to write an index. The file is private to the owner and
/// invisible to readers until <see cref="Publish"/> swaps the pointer; disposing without publishing deletes it.
/// </summary>
internal sealed partial class SqliteIndexBuild(BuildTarget target, SqliteConnection connection, IndexHeader? previous) : IIndexBuild
{
    private bool _published;
    private bool _closed;

    public bool Incremental { get; } = previous is not null;

    public IndexHeader? Previous { get; } = previous;

    public ValueTask<Result<IReadOnlyList<StoredFile>>> StoredFiles(CancellationToken cancellationToken)
        => SqliteFailure.Guard("Reading the stored files", async () => await ReadStoredFiles(cancellationToken).ConfigureAwait(false));

    public ValueTask<Result<Unit>> ReplaceDeps(IReadOnlyList<DepRecord> deps, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deps);
        return SqliteFailure.Guard("Replacing the dependencies", async () => await WriteDeps(deps, cancellationToken).ConfigureAwait(false));
    }

    public ValueTask<Result<Unit>> SetLayer(LayerRecord layer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return SqliteFailure.Guard("Recording a layer", async () => await WriteLayer(layer, cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask DisposeAsync()
    {
        if (!_closed)
        {
            _closed = true;
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        if (!_published)
        {
            IndexFiles.DeleteWithSidecars(target.Files.DbPath(target.Repo, target.Ulid));
            target.Files.ClearBuild(target.Repo, target.Ulid);
        }

        await target.Hold.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<Result<IReadOnlyList<StoredFile>>> ReadStoredFiles(CancellationToken cancellationToken)
    {
        var stored = new List<StoredFile>();
        var command = Sql.Command(connection, "SELECT path, blob, parsed FROM files ORDER BY path");
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    stored.Add(new StoredFile(reader.GetString(0), reader.GetString(1), reader.GetInt32(2) != 0));
                }
            }
        }

        return stored;
    }

    private async Task<Result<Unit>> WriteDeps(IReadOnlyList<DepRecord> deps, CancellationToken cancellationToken)
    {
        var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            _ = await Sql.Run(connection, "DELETE FROM deps", cancellationToken, transaction).ConfigureAwait(false);
            var insert = new Stmt(connection, transaction, "INSERT INTO deps(manifest, name, version, kind, tags) VALUES ($manifest, $name, $version, $kind, $tags)", "$manifest", "$name", "$version", "$kind", "$tags");
            await using (insert.ConfigureAwait(false))
            {
                foreach (var dep in deps)
                {
                    _ = await insert.Set("$manifest", dep.Manifest).Set("$name", dep.Name).Set("$version", dep.Version).Set("$kind", dep.Kind).Set("$tags", dep.Tags).Run(cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }

    private async Task<Result<Unit>> WriteLayer(LayerRecord layer, CancellationToken cancellationToken)
    {
        var command = Sql.Command(connection, "INSERT OR REPLACE INTO layers(layer, stamp, status, code, detail, built_at) VALUES ($layer, $stamp, $status, $code, $detail, $at)")
            .With("$layer", StoredNames.Text(layer.Layer))
            .With("$stamp", layer.Stamp)
            .With("$status", StoredNames.Text(layer.State))
            .With("$code", layer.Code)
            .With("$detail", layer.Detail)
            .With("$at", layer.BuiltAt.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await using (command.ConfigureAwait(false))
        {
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
