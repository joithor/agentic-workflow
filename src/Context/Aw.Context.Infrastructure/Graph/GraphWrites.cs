using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The graph store's writes, each statement list in one write transaction.</summary>
/// <param name="sessions">The migrated store's session factory.</param>
/// <param name="batchSize">Statements per transaction while a version is written.</param>
internal sealed class GraphWrites(SqliteUnitOfWorkFactory sessions, int batchSize)
{
    /// <summary>
    /// Writes <paramref name="map"/> as a 'building' version in batches, then retires the current one and makes it current
    /// (with the file's overrides) in one transaction. Leftover 'building' versions (an earlier crash) are removed first;
    /// a failed or cancelled save removes its own.
    /// </summary>
    public async ValueTask<Result<MapVersion>> Save(MapImport map, DateTimeOffset created, CancellationToken cancellationToken)
    {
        var swept = await Run([new SqlStatement(GraphSql.DeleteBuilding)], returnsId: false, cancellationToken).ConfigureAwait(false);
        var begun = swept.IsError ? swept : await Run([new SqlStatement(GraphSql.InsertVersion, c => c.Bind("$repo", map.Repo.Value).Bind("$commit", map.Commit)
            .Bind("$keyVersion", EntryKey.Version).Bind("$created", created))], returnsId: true, cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var version = begun.Value;
        try
        {
            var written = await Write(map, version, cancellationToken).ConfigureAwait(false);
            return written.IsError
                ? await Abandon(version, written.Error).ConfigureAwait(false)
                : new MapVersion((int)version, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Current);
        }
        catch (Exception)
        {
            _ = await Abandon(version, error: null).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Deletes retained versions (never a current or building one); their graphs go with them (ON DELETE CASCADE).</summary>
    public async ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken)
    {
        var deleted = await Run([.. versions.Select(v => new SqlStatement(GraphSql.DeleteRetained, c => c.Bind("$v", (long)v)))], returnsId: false, cancellationToken).ConfigureAwait(false);
        return deleted.IsError ? deleted.Error : (int)deleted.Value;
    }

    private async ValueTask<Result<long>> Write(MapImport map, long version, CancellationToken cancellationToken)
    {
        foreach (var batch in GraphStatements.Graph(version, map.Graph).Chunk(batchSize))
        {
            var written = await Run(batch, returnsId: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }
        }

        return await Flip(map, version, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retires the current version, makes this one current and adds the file's overrides, in one transaction that commits
    /// only when this version was still 'building' (another import's sweep may have removed it); otherwise nothing changes.
    /// </summary>
    internal async ValueTask<Result<long>> Flip(MapImport map, long version, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var building = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.IsBuilding, c => c.Bind("$v", version)), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (building.IsError || building.Value != 1)
            {
                return building.IsError ? ContextDatabase.Translate(building.Error)
                    : ContextErrors.StoreUnavailable.With("another import removed this one before it became current; nothing changed (fix: import again)");
            }

            SqlStatement[] flip = [new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrent, c => c.Bind("$v", version)), .. map.Overrides.Select(GraphStatements.Override)];
            foreach (var statement in flip)
            {
                var done = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return ContextDatabase.Translate(done.Error);
                }
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : version;
        }
    }

    /// <summary>Removes a version that didn't become current, whatever the caller's token says; reports both failures if that fails too.</summary>
    private async ValueTask<Result<MapVersion>> Abandon(long version, Error? error)
    {
        var removed = await Run([new SqlStatement(GraphSql.DeleteBuildingVersion, c => c.Bind("$v", version))], returnsId: false, CancellationToken.None).ConfigureAwait(false);
        var cause = error is null ? "the save stopped" : $"{error.Code}: {error.Message}";
        return removed.IsError
            ? ContextErrors.StoreUnavailable.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{removed.Error.Code}: {removed.Error.Message}, while removing building version {version} after {cause}"))
            : error ?? ContextErrors.StoreUnavailable.With("the save stopped; nothing became current");
    }

    /// <summary>Runs <paramref name="statements"/> in one write transaction: the id the first returns, or the rows they changed.</summary>
    private async ValueTask<Result<long>> Run(IReadOnlyList<SqlStatement> statements, bool returnsId, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            long total = 0;
            foreach (var statement in statements)
            {
                var step = returnsId
                    ? await SqliteQueries.One(begun.Value, statement, GraphRows.Id, cancellationToken).ConfigureAwait(false)
                    : (await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false)).Map(n => (long)n);
                if (step.IsError)
                {
                    return ContextDatabase.Translate(step.Error);
                }

                total = returnsId ? step.Value : total + step.Value;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : total;
        }
    }
}
