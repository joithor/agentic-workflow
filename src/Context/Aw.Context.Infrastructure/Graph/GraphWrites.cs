using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
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
    public async ValueTask<Result<MapVersion>> Save(MapImport map, DateTimeOffset created, CancellationToken cancellationToken, bool draft = false)
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
            var written = await Write(map, version, draft, cancellationToken).ConfigureAwait(false);
            return written.IsError
                ? await Abandon(version, written.Error).ConfigureAwait(false)
                : new MapVersion((int)version, map.Repo, map.Commit, EntryKey.Version, created, draft ? MapVersionStatus.Draft : MapVersionStatus.Current);
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

    private async ValueTask<Result<long>> Write(MapImport map, long version, bool draft, CancellationToken cancellationToken)
    {
        foreach (var batch in GraphStatements.Graph(version, map.Graph).Chunk(batchSize))
        {
            var written = await Run(batch, returnsId: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }
        }

        return await Flip(map, version, cancellationToken, draft).ConfigureAwait(false);
    }

    /// <summary>
    /// Retires the current version (or, for a draft, deletes the open draft), makes this one current (or the draft) and adds the file's overrides, in one transaction that commits
    /// only when this version was still 'building' (another import's sweep may have removed it); otherwise nothing changes.
    /// </summary>
    internal async ValueTask<Result<long>> Flip(MapImport map, long version, CancellationToken cancellationToken, bool draft = false)
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

            SqlStatement[] switchOver = draft
                ? [new(GraphSql.DeleteDrafts), new(GraphSql.MakeDraft, c => c.Bind("$v", version))]
                : [new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrent, c => c.Bind("$v", version))];
            SqlStatement[] flip = [.. switchOver, .. map.Overrides.Select(GraphStatements.Override)];
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

    /// <summary>The open draft, or a copy of the current version made in one transaction when there is none.</summary>
    public async ValueTask<Result<MapVersion>> OpenDraft(DateTimeOffset created, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var versions = await SqliteQueries.List(begun.Value, new SqlStatement(GraphSql.SelectVersions), GraphRows.Version, cancellationToken).ConfigureAwait(false);
            if (versions.IsError)
            {
                return ContextDatabase.Translate(versions.Error);
            }

            if (versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Draft) is { } open)
            {
                return open;
            }

            if (versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Current) is not { } current)
            {
                return ContextErrors.NoMap.With("there is no current map to open a draft from (fix: run 'sindri context map build' or import a map file)");
            }

            var copy = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.CopyVersion, c => c.Bind("$created", created).Bind("$from", (long)current.Number)), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (copy.IsError)
            {
                return ContextDatabase.Translate(copy.Error);
            }

            var copied = await CopyGraph(begun.Value, current.Number, copy.Value, cancellationToken).ConfigureAwait(false);
            if (copied.IsError)
            {
                return ContextDatabase.Translate(copied.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : current with { Number = (int)copy.Value, Created = created, Status = MapVersionStatus.Draft };
        }
    }

    private static async ValueTask<Result<Unit>> CopyGraph(ISqliteSession session, int from, long to, CancellationToken cancellationToken)
    {
        foreach (var sql in GraphSql.CopyGraph)
        {
            var copied = await SqliteQueries.Execute(session, new SqlStatement(sql, c => c.Bind("$to", to).Bind("$from", (long)from)), cancellationToken).ConfigureAwait(false);
            if (copied.IsError)
            {
                return copied.Error;
            }
        }

        return Unit.Value;
    }

    /// <summary>Retires the current version and makes the draft current, in one transaction; anything but a draft is SND-CONTEXT-046.</summary>
    public async ValueTask<Result<MapVersion>> Promote(int draft, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var isDraft = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.VersionStatus, c => c.Bind("$v", (long)draft)), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (isDraft.IsError || isDraft.Value != 1)
            {
                return isDraft.IsError ? ContextDatabase.Translate(isDraft.Error)
                    : ContextErrors.PromoteRefused.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"version {draft} isn't a draft (fix: list the versions with 'sindri context map status')"));
            }

            foreach (var statement in (SqlStatement[])[new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrentDraft, c => c.Bind("$v", (long)draft))])
            {
                var done = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return ContextDatabase.Translate(done.Error);
                }
            }

            var version = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.SelectVersion, c => c.Bind("$v", (long)draft)), GraphRows.Version, cancellationToken).ConfigureAwait(false);
            if (version.IsError)
            {
                return ContextDatabase.Translate(version.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : version.Value;
        }
    }

    /// <summary>Adds the overrides in one transaction (all or none); one already there is kept once.</summary>
    public async ValueTask<Result<int>> AppendOverrides(IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken)
    {
        var added = await Run([.. overrides.Select(GraphStatements.Override)], returnsId: false, cancellationToken).ConfigureAwait(false);
        return added.IsError ? added.Error : (int)added.Value;
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
