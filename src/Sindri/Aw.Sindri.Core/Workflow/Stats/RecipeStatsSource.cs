using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// A recipe's stats for one request (API spec §3.8, P12): the views.db cache first for a recipe that is not pinned as a child, else counted from the
/// ledger's workflow rows, read once per request however many recipes it asks about, and kept. A hit is the stats as counted, restamped with this read's time.
/// A recipe pinned as a child is never cached: whether it is child-only turns on the rows, not on a run's start or end.
/// </summary>
/// <param name="rows">The ledger's workflow rows.</param>
/// <param name="clock">The read's time.</param>
/// <param name="cache">The stats cache; null counts on every read.</param>
public sealed class RecipeStatsSource(IWorkflowStatRows rows, TimeProvider clock, IRecipeStatsCache? cache = null)
{
    private (long Head, IReadOnlyList<StatEvent> Events)? _snapshot;

    /// <summary>The stats of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The registered recipe.</param>
    /// <param name="hash">A recipe hash prefix, or null for every version.</param>
    /// <param name="window">The window.</param>
    /// <param name="pinned">Whether some recipe starts <paramref name="recipe"/> as a child.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The stats; a pinned recipe that never started as a root is child-only.</returns>
    public async ValueTask<Result<RecipeStats>> Of(RecipeId recipe, string? hash, StatsWindow window, bool pinned, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        var now = clock.GetUtcNow();
        var keeping = pinned ? null : cache;
        if (keeping is { } reader)
        {
            var kept = await reader.Get(recipe, hash, window, now, cancellationToken).ConfigureAwait(false);
            if (kept is { IsSuccess: true, Value: { } hit })
            {
                return hit with { AsOf = now };
            }
        }

        var snapshot = await Snapshot(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError)
        {
            return snapshot.Error;
        }

        var (head, events) = snapshot.Value;
        if (pinned && !RecipeStatsCounter.HasRoot(events, recipe))
        {
            return RecipeStats.ForChildOnly(recipe, hash, window, now);
        }

        var counted = RecipeStatsCounter.Count(events, recipe, hash, window, now);
        if (keeping is { } writer)
        {
            _ = await writer.Put(counted, head, now, cancellationToken).ConfigureAwait(false);   // best effort: a read that can't keep its result still answers
        }

        return counted;
    }

    private async ValueTask<Result<(long Head, IReadOnlyList<StatEvent> Events)>> Snapshot(CancellationToken cancellationToken)
    {
        if (_snapshot is { } taken)
        {
            return taken;
        }

        var head = await rows.Head(cancellationToken).ConfigureAwait(false);   // before the rows, so the rows are no older than the head
        var read = head.IsError ? head.Error : await rows.Read(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        _snapshot = (head.Value, read.Value);
        return _snapshot.Value;
    }
}
