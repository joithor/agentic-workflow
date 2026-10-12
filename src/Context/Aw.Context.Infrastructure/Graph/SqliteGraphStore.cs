using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The graph store in context.db: versions are written in batches as 'building', then made current in one short transaction.</summary>
internal sealed class SqliteGraphStore(ContextDatabase database) : IGraphStore
{
    private const int BatchSize = 500;

    public async ValueTask<Result<MapVersion?>> Current(CancellationToken cancellationToken)
    {
        var versions = await Versions(cancellationToken).ConfigureAwait(false);
        return versions.IsError ? versions.Error : Results.Success(versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Current));
    }

    public ValueTask<Result<IReadOnlyList<MapVersion>>> Versions(CancellationToken cancellationToken) =>
        Query(new SqlStatement(GraphSql.SelectVersions), GraphRows.Version, cancellationToken);

    public ValueTask<Result<IReadOnlyList<MapOverride>>> Overrides(CancellationToken cancellationToken) =>
        Query(new SqlStatement(GraphSql.SelectOverrides), GraphRows.Override, cancellationToken);

    public async ValueTask<Result<GraphSnapshot>> Read(int version, CancellationToken cancellationToken)
    {
        var open = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (open.IsError || open.Value is not { } sessions)
        {
            return open.IsError ? open.Error : GraphSnapshot.Empty;
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var graph = await GraphReads.Version(read.Value, version, cancellationToken).ConfigureAwait(false);
            return graph.IsError ? ContextDatabase.Translate(graph.Error) : graph;
        }
    }

    public async ValueTask<Result<MapVersion>> SaveCurrent(MapImport map, DateTimeOffset created, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(map);
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var begun = await Run(open.Value, [new SqlStatement(GraphSql.InsertVersion, c => c.Bind("$repo", map.Repo).Bind("$commit", map.Commit)
            .Bind("$keyVersion", EntryKey.Version).Bind("$created", created))], returnsId: true, cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var version = begun.Value;
        foreach (var batch in GraphStatements.Graph(version, map.Graph).Chunk(BatchSize))
        {
            var written = await Run(open.Value, batch, returnsId: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                _ = await Run(open.Value, [Delete(version)], returnsId: false, CancellationToken.None).ConfigureAwait(false);
                return written.Error;
            }
        }

        SqlStatement[] flip = [new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrent, c => c.Bind("$v", version)), .. map.Overrides.Select(GraphStatements.Override)];
        var flipped = await Run(open.Value, flip, returnsId: false, cancellationToken).ConfigureAwait(false);
        return flipped.IsError
            ? flipped.Error
            : new MapVersion((int)version, map.Repo, map.Commit, EntryKey.Version, created, MapVersionStatus.Current);
    }

    public async ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
        {
            return 0;
        }

        var open = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (open.IsError || open.Value is not { } sessions)
        {
            return open.IsError ? open.Error : 0;
        }

        var deleted = await Run(sessions, [.. versions.Select(v => Delete(v))], returnsId: false, cancellationToken).ConfigureAwait(false);
        return deleted.IsError ? deleted.Error : (int)deleted.Value;
    }

    private static SqlStatement Delete(long version) => new(GraphSql.DeleteVersion, c => c.Bind("$v", version));

    /// <summary>Runs <paramref name="statements"/> in one write transaction: the id the first returns, or the rows they changed.</summary>
    private static async ValueTask<Result<long>> Run(SqliteUnitOfWorkFactory sessions, IReadOnlyList<SqlStatement> statements, bool returnsId, CancellationToken cancellationToken)
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

    private async ValueTask<Result<IReadOnlyList<T>>> Query<T>(SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var open = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (open.IsError || open.Value is not { } sessions)
        {
            return open.IsError ? open.Error : Results.Success<IReadOnlyList<T>>([]);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, statement, mapper, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }
}
