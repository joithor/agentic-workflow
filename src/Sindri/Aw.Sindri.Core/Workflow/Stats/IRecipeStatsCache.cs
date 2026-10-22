using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// The recipe stats cache in views.db (API spec §3.8, P12): counted stats kept by recipe, window and hash. A run's start or end empties it
/// (the projector), a rebuild leaves it empty, and an entry older than <see cref="MaxAge"/> is not served, because a window slides with the clock.
/// </summary>
public interface IRecipeStatsCache
{
    /// <summary>How long an entry is served: the longest a run can stay counted after it left a window, or stay out of one it entered.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(1);

    /// <summary>The stats kept for the key, or null when there are none, they are older than <see cref="MaxAge"/> or the views store doesn't exist.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="hash">The hash the stats are scoped to, as asked; null for every version.</param>
    /// <param name="window">The window.</param>
    /// <param name="now">The read's time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The stats as counted, with the <c language="text">AsOf</c> of the counting; the caller restamps it.</returns>
    public ValueTask<Result<RecipeStats?>> Get(RecipeId recipe, string? hash, StatsWindow window, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Keeps <paramref name="stats"/>, unless the projector has folded past <paramref name="head"/> (an end it did not count may have emptied the cache) or no views store exists.</summary>
    /// <param name="stats">The stats as counted.</param>
    /// <param name="head">The ledger head read before the rows were counted.</param>
    /// <param name="now">The counting's time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, also when nothing was kept.</returns>
    public ValueTask<Result<Unit>> Put(RecipeStats stats, long head, DateTimeOffset now, CancellationToken cancellationToken);
}
