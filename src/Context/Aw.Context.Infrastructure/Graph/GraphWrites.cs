using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
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
    public async ValueTask<Result<MapVersion>> Save(MapImport map, DateTimeOffset created, CancellationToken cancellationToken, bool draft = false, int? replacing = null)
    {
        if (map.Heads is { } heads && MapHead.Check(heads) is { } refused)
        {
            return refused;
        }

        // A replacement sweeps nothing: a 'building' version is another build's, still being written.
        var swept = replacing is null ? await Run([new SqlStatement(GraphSql.DeleteBuilding)], returnsId: false, cancellationToken).ConfigureAwait(false) : Results.Success(0L);
        var begun = swept.IsError ? swept : await Run([new SqlStatement(GraphSql.InsertVersion, c => c.Bind("$repo", map.Repo.Value).Bind("$commit", map.Commit)
            .Bind("$keyVersion", EntryKey.Version).Bind("$created", created))], returnsId: true, cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var version = begun.Value;
        try
        {
            var written = await Write(map, version, draft, replacing, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<Result<long>> Write(MapImport map, long version, bool draft, int? replacing, CancellationToken cancellationToken)
    {
        foreach (var batch in GraphStatements.Graph(version, map.Graph).Chunk(batchSize))
        {
            var written = await Run(batch, returnsId: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }
        }

        return await Flip(map, version, cancellationToken, draft, replacing).ConfigureAwait(false);
    }

    /// <summary>
    /// Retires the current version (or, for a draft, deletes the open draft), makes this one current (or the draft) and adds the file's overrides, in one transaction that commits
    /// only when this version was still 'building' (another import's sweep may have removed it); otherwise nothing changes.
    /// </summary>
    internal async ValueTask<Result<long>> Flip(MapImport map, long version, CancellationToken cancellationToken, bool draft = false, int? replacing = null)
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
                    : replacing is not null ? ContextErrors.PromoteRefused.With("the draft is no longer the open draft: another build removed the copy being written; nothing was replaced")
                    : ContextErrors.StoreUnavailable.With("another import removed this one before it became current; nothing changed (fix: import again)");
            }

            var switched = await SwitchOver(begun.Value, map, (version, draft, replacing), cancellationToken).ConfigureAwait(false);
            if (switched.IsError)
            {
                return switched.Error;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : version;
        }
    }

    // The statements that make the version current (or the draft) and add the file's overrides; a replacement removes only the draft it read, and 046 when that is gone.
    private static async ValueTask<Result<Unit>> SwitchOver(ISqliteSession session, MapImport map, (long Version, bool Draft, int? Replacing) target, CancellationToken cancellationToken)
    {
        var (version, draft, replacing) = target;
        // The heads first: a replacement copies the replaced draft's before that draft (and its rows) is deleted.
        foreach (var head in HeadStatements(map, version, draft, replacing))
        {
            var written = await SqliteQueries.Execute(session, head, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return ContextDatabase.Translate(written.Error);
            }
        }

        if (replacing is { } source)
        {
            var removed = await SqliteQueries.Execute(session, new SqlStatement(GraphSql.DeleteDraft, c => c.Bind("$v", (long)source)), cancellationToken).ConfigureAwait(false);
            if (removed.IsError || removed.Value != 1)
            {
                return removed.IsError ? ContextDatabase.Translate(removed.Error)
                    : ContextErrors.PromoteRefused.With("the draft is no longer the open draft; nothing was replaced");
            }
        }

        SqlStatement[] switchOver = draft
            ? [.. replacing is null ? (SqlStatement[])[new(GraphSql.DeleteDrafts)] : [], new(GraphSql.MakeDraft, c => c.Bind("$v", version))]
            : [new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrent, c => c.Bind("$v", version))];
        // A new current version ends any wait: the hold and the chain go with it (a draft's hold goes with the draft it names).
        SqlStatement[] clear = draft ? [] : [new(GraphSql.DeleteHolds), new(GraphSql.DeleteChain)];
        foreach (var statement in (SqlStatement[])[.. switchOver, .. clear, .. map.Overrides.Select(GraphStatements.Override)])
        {
            var done = await SqliteQueries.Execute(session, statement, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return ContextDatabase.Translate(done.Error);
            }
        }

        return Unit.Value;
    }

    // What the version records of where its repos stood: the build's own heads, else a replaced draft's, else (an import) the one commit it names.
    private static IEnumerable<SqlStatement> HeadStatements(MapImport map, long version, bool draft, int? replacing)
    {
        if (map.Heads is { } heads)
        {
            return heads.Select(h => new SqlStatement(GraphSql.InsertHead, c => c.Bind("$v", version).Bind("$repo", h.Repo.Value).Bind("$commit", h.Commit).Bind("$onDefault", h.OnDefault ? 1L : 0L)));
        }

        if (replacing is { } source)
        {
            return [new SqlStatement(GraphSql.CopyHeads, c => c.Bind("$to", version).Bind("$from", (long)source))];
        }

        return !draft && map.Commit is { } commit && MapHead.IsCommit(commit)
            ? [new SqlStatement(GraphSql.InsertHead, c => c.Bind("$v", version).Bind("$repo", map.Repo.Value).Bind("$commit", commit).Bind("$onDefault", 1L))]
            : [];
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
                return ContextErrors.NoMap.With("there is no current map to open a draft from (fix: sindri context map import <file>)");
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

            foreach (var statement in (SqlStatement[])[new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrentDraft, c => c.Bind("$v", (long)draft)), new(GraphSql.DeleteHolds), new(GraphSql.DeleteChain)])
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

    /// <summary>
    /// Adds the overrides in one transaction (all or none); one already there is kept once. With <paramref name="expectedDraft"/> the transaction first checks that it is the latest draft (190 otherwise).
    /// </summary>
    public async ValueTask<Result<int>> AppendOverrides(IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken, int? expectedDraft = null)
    {
        if (expectedDraft is { } expected)
        {
            return await AppendFor(expected, overrides, cancellationToken).ConfigureAwait(false);
        }

        var added = await Run([.. overrides.Select(GraphStatements.Override)], returnsId: false, cancellationToken).ConfigureAwait(false);
        return added.IsError ? added.Error : (int)added.Value;
    }

    private async ValueTask<Result<int>> AppendFor(int expected, IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var latest = await SqliteQueries.List(begun.Value, new SqlStatement(GraphSql.LatestDraft), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (latest.IsError)
            {
                return ContextDatabase.Translate(latest.Error);
            }

            if (latest.Value.Count == 0 || latest.Value[0] != expected)
            {
                return ContextErrors.DraftChanged.With(Changed(expected, latest.Value.Count == 0 ? null : (int)latest.Value[0]));
            }

            long added = 0;
            foreach (var statement in overrides.Select(GraphStatements.Override))
            {
                var done = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return ContextDatabase.Translate(done.Error);
                }

                added += done.Value;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : (int)added;
        }
    }

    /// <summary>The words of a 190: which draft was expected and which is the latest.</summary>
    internal static string Changed(int expected, int? latest) => string.Create(System.Globalization.CultureInfo.InvariantCulture,
        $"draft {expected} was expected, {(latest is { } n ? $"the latest draft is {n}" : "there is no draft")} (a rebuild replaced it, or it was promoted)");

    /// <summary>
    /// Holds the latest draft: checks it is the draft named, reads the wait's start from the chain row, writes the chain (clamped to now) and the hold, in one transaction.
    /// </summary>
    public async ValueTask<Result<MapHold>> Hold(MapHold hold, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var latest = await SqliteQueries.List(begun.Value, new SqlStatement(GraphSql.LatestDraft), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (latest.IsError)
            {
                return ContextDatabase.Translate(latest.Error);
            }

            if (latest.Value.Count == 0 || latest.Value[0] != hold.Version)
            {
                return ContextErrors.DraftChanged.With(Changed(hold.Version, latest.Value.Count == 0 ? null : (int)latest.Value[0]));
            }

            var chain = await SqliteQueries.List(begun.Value, new SqlStatement(GraphSql.SelectChain), GraphRows.Since, cancellationToken).ConfigureAwait(false);
            if (chain.IsError)
            {
                return ContextDatabase.Translate(chain.Error);
            }

            var since = MapHold.NextSince(chain.Value.Count == 0 ? null : GraphText.Time(chain.Value[0]), hold.Since);
            SqlStatement[] writes =
            [
                new(GraphSql.UpsertChain, c => c.Bind("$since", GraphText.Time(since))),
                new(GraphSql.DeleteHolds),
                new(GraphSql.InsertHold, c => c.Bind("$v", (long)hold.Version)),
            ];
            foreach (var statement in writes)
            {
                var done = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return ContextDatabase.Translate(done.Error);
                }
            }

            var heads = await SqliteQueries.List(begun.Value, new SqlStatement(GraphSql.SelectHeads, c => c.Bind("$v", (long)hold.Version)), GraphRows.Head, cancellationToken).ConfigureAwait(false);
            if (heads.IsError)
            {
                return ContextDatabase.Translate(heads.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : new MapHold(hold.Version, since, heads.Value);
        }
    }

    /// <summary>Removes the hold on a draft (the chain stays).</summary>
    public async ValueTask<Result<Unit>> Release(int version, CancellationToken cancellationToken)
    {
        var released = await Run([new SqlStatement(GraphSql.DeleteHold, c => c.Bind("$v", (long)version))], returnsId: false, cancellationToken).ConfigureAwait(false);
        return released.IsError ? released.Error : Unit.Value;
    }

    /// <summary>Adds a scoping run's 'propose' rows and replaces its surface list in one transaction (all or none).</summary>
    public async ValueTask<Result<int>> Propose(ScopeProposal proposal, IReadOnlyList<MapOverride> rows, CancellationToken cancellationToken)
    {
        var written = await Run([.. rows.Select(GraphStatements.Override), GraphStatements.Run(proposal)], returnsId: false, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : (int)written.Value - 1;   // the total counts the run's one upserted row
    }

    /// <summary>Remembers a scoping run's surface list, replacing an earlier one of the same run.</summary>
    public async ValueTask<Result<int>> SaveRun(ScopeProposal proposal, CancellationToken cancellationToken)
    {
        var saved = await Run([GraphStatements.Run(proposal)], returnsId: false, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : proposal.Surfaces.Count;
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
