using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

internal sealed partial class GraphWrites
{
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
            var latest = await RequireLatest(begun.Value, expected, cancellationToken).ConfigureAwait(false);
            if (latest.IsError)
            {
                return latest.Error;
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
            var latest = await RequireLatest(begun.Value, hold.Version, cancellationToken).ConfigureAwait(false);
            var stored = latest.IsError ? latest.Error : await Store(begun.Value, hold, cancellationToken).ConfigureAwait(false);
            if (stored.IsError)
            {
                return stored.Error;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : stored.Value;
        }
    }

    // The 190 check: the draft the caller named is the latest one, read in the same transaction as the write that follows.
    private static async ValueTask<Result<Unit>> RequireLatest(ISqliteSession session, int expected, CancellationToken cancellationToken)
    {
        var latest = await SqliteQueries.List(session, new SqlStatement(GraphSql.LatestDraft), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return ContextDatabase.Translate(latest.Error);
        }

        return latest.Value.Count == 0 || latest.Value[0] != expected
            ? ContextErrors.DraftChanged.With(Changed(expected, latest.Value.Count == 0 ? null : (int)latest.Value[0]))
            : Unit.Value;
    }

    // The chain's start survives a replaced hold (clamped to now), then the hold itself, then the heads the hold carries back.
    private static async ValueTask<Result<MapHold>> Store(ISqliteSession session, MapHold hold, CancellationToken cancellationToken)
    {
        var chain = await SqliteQueries.List(session, new SqlStatement(GraphSql.SelectChain), GraphRows.Since, cancellationToken).ConfigureAwait(false);
        if (chain.IsError)
        {
            return ContextDatabase.Translate(chain.Error);
        }

        var since = MapHold.NextSince(chain.Value.Count == 0 ? null : GraphText.Time(chain.Value[0]), hold.Since);
        var written = await Execute(session, [new(GraphSql.UpsertChain, c => c.Bind("$since", GraphText.Time(since))), new(GraphSql.DeleteHolds), new(GraphSql.InsertHold, c => c.Bind("$v", (long)hold.Version))], cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        var heads = await SqliteQueries.List(session, new SqlStatement(GraphSql.SelectHeads, c => c.Bind("$v", (long)hold.Version)), GraphRows.Head, cancellationToken).ConfigureAwait(false);
        return heads.IsError ? ContextDatabase.Translate(heads.Error) : new MapHold(hold.Version, since, heads.Value);
    }

    /// <summary>Removes the hold on a draft (the chain stays).</summary>
    public async ValueTask<Result<Unit>> Release(int version, CancellationToken cancellationToken)
    {
        var released = await Run([new SqlStatement(GraphSql.DeleteHold, c => c.Bind("$v", (long)version))], returnsId: false, cancellationToken).ConfigureAwait(false);
        return released.IsError ? released.Error : Unit.Value;
    }
}
