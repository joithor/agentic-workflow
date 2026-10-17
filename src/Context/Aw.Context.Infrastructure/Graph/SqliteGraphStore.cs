using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>
/// The graph store in context.db. Reads open the file read-only and never migrate it (doctor runs them from the CLI): a
/// missing file, or one whose schema has no graph tables yet, is a store with no map. Writes migrate first; a version is
/// written in batches as 'building', then made current in one short transaction, and removed again when anything fails.
/// </summary>
internal sealed class SqliteGraphStore(ContextDatabase database, ContextStoreSettings settings) : IGraphStore, IProposalRuns
{
    private const int BatchSize = 500;

    public async ValueTask<Result<MapVersion?>> Current(CancellationToken cancellationToken)
    {
        var versions = await Versions(cancellationToken).ConfigureAwait(false);
        return versions.IsError ? versions.Error : Results.Success(versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Current));
    }

    public ValueTask<Result<IReadOnlyList<MapVersion>>> Versions(CancellationToken cancellationToken) =>
        Query(new SqlStatement(GraphSql.SelectVersions), GraphRows.Version, cancellationToken);

    public async ValueTask<Result<IReadOnlyList<MapOverride>>> Overrides(CancellationToken cancellationToken)
    {
        var read = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<MapOverride>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await ReadOverrides(session, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    public async ValueTask<Result<MapSnapshot?>> Snapshot(bool draft, CancellationToken cancellationToken)
    {
        var read = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<MapSnapshot?>(value: null);
        }

        await using (session.ConfigureAwait(false))
        {
            var snapshot = await SnapshotIn(session, draft ? MapVersionStatus.Draft : MapVersionStatus.Current, cancellationToken).ConfigureAwait(false);
            return snapshot.IsError ? ContextDatabase.Translate(snapshot.Error) : snapshot;
        }
    }

    public ValueTask<Result<MapSnapshot?>> SnapshotAt(int version, CancellationToken cancellationToken) => throw new NotSupportedException();

    public async ValueTask<Result<GraphSnapshot>> Read(int version, CancellationToken cancellationToken)
    {
        var read = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : GraphSnapshot.Empty;
        }

        await using (session.ConfigureAwait(false))
        {
            var graph = await GraphReads.Version(session, version, cancellationToken).ConfigureAwait(false);
            return graph.IsError ? ContextDatabase.Translate(graph.Error) : graph;
        }
    }

    public async ValueTask<Result<MapVersion>> SaveCurrent(MapImport map, DateTimeOffset created, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).Save(map, created, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0 || !await HasGraph(cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).Delete(versions, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> HasGraph(CancellationToken cancellationToken)
    {
        var read = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError;   // an unreadable store goes on to the write, which reports why
        }

        await session.DisposeAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>The newest version with <paramref name="status"/>, its graph and the overrides, all read in <paramref name="session"/>.</summary>
    private static async ValueTask<Result<MapSnapshot?>> SnapshotIn(SqliteReadSession session, MapVersionStatus status, CancellationToken cancellationToken)
    {
        var versions = await SqliteQueries.List(session, new SqlStatement(GraphSql.SelectVersions), GraphRows.Version, cancellationToken).ConfigureAwait(false);
        if (versions.IsError || versions.Value.FirstOrDefault(v => v.Status == status) is not { } version)
        {
            return versions.IsError ? versions.Error : Results.Success<MapSnapshot?>(value: null);
        }

        var graph = await GraphReads.Version(session, version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await ReadOverrides(session, cancellationToken).ConfigureAwait(false);
        return graph.IsError ? graph.Error : overrides.IsError ? overrides.Error : new MapSnapshot(version, graph.Value, overrides.Value);
    }

    /// <summary>Every override; a store not yet upgraded to migration 0004 (reads never migrate) answers with the new fields empty.</summary>
    private static async ValueTask<Result<IReadOnlyList<MapOverride>>> ReadOverrides(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var upgraded = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasReviewColumns), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        return upgraded.IsError
            ? upgraded.Error
            : await SqliteQueries.List(session, new SqlStatement(upgraded.Value > 0 ? GraphSql.SelectOverrides : GraphSql.SelectOverridesBefore0004), GraphRows.Override, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A read-only session when the file exists and has the graph tables; null for 'no map yet'.</summary>
    private async ValueTask<Result<SqliteReadSession?>> OpenRead(CancellationToken cancellationToken)
    {
        if (!File.Exists(settings.DatabasePath))
        {
            return (SqliteReadSession?)null;
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        var tables = await SqliteQueries.One(read.Value, new SqlStatement(GraphSql.CountGraphTables), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        if (tables.IsSuccess && tables.Value > 0)
        {
            return read.Value;
        }

        await read.Value.DisposeAsync().ConfigureAwait(false);
        return tables.IsError ? ContextDatabase.Translate(tables.Error) : (SqliteReadSession?)null;
    }

    public async ValueTask<Result<MapVersion>> SaveDraft(MapImport map, DateTimeOffset created, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).Save(map, created, cancellationToken, draft: true).ConfigureAwait(false);
    }

    public async ValueTask<Result<MapVersion>> ReplaceDraft(int draft, MapImport map, DateTimeOffset created, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).Save(map, created, cancellationToken, draft: true, replacing: draft).ConfigureAwait(false);
    }

    public async ValueTask<Result<MapVersion>> OpenDraft(DateTimeOffset created, CancellationToken cancellationToken)
    {
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).OpenDraft(created, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<MapVersion>> Promote(int draft, CancellationToken cancellationToken)
    {
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).Promote(draft, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<int>> AppendOverrides(IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        if (overrides.Count == 0)
        {
            return 0;
        }

        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).AppendOverrides(overrides, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<int>> SaveRun(ScopeProposal proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        return open.IsError ? open.Error : await new GraphWrites(open.Value, BatchSize).SaveRun(proposal, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<int>> Propose(ScopeProposal proposal, IReadOnlyList<MapOverride> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(rows);
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        return await new GraphWrites(open.Value, BatchSize).Propose(proposal, rows, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<ScopeProposal?>> Run(string run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var read = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<ScopeProposal?>(value: null);
        }

        await using (session.ConfigureAwait(false))
        {
            var table = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasRunTable), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            var rows = table.IsError || table.Value == 0 ? null : await SqliteQueries.List(session, new SqlStatement(GraphSql.SelectRun, c => c.Bind("$run", run)), GraphRows.Run, cancellationToken).ConfigureAwait(false);
            return table.IsError ? ContextDatabase.Translate(table.Error)
                : rows is null ? Results.Success<ScopeProposal?>(value: null)
                : rows.IsError ? ContextDatabase.Translate(rows.Error)
                : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
        }
    }

    private async ValueTask<Result<IReadOnlyList<T>>> Query<T>(SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var read = await OpenRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } session)
        {
            return read.IsError ? read.Error : Results.Success<IReadOnlyList<T>>([]);
        }

        await using (session.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(session, statement, mapper, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }
}
