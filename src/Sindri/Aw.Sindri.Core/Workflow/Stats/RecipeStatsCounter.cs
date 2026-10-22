using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Counts a recipe's stats from its workflow ledger rows (API spec §3.8, E-24): a pure function of the rows, the window and the clock.</summary>
public static class RecipeStatsCounter
{
    /// <summary>The stats of <paramref name="recipe"/> for the runs that started inside <paramref name="window"/> before <paramref name="now"/>.</summary>
    /// <param name="events">The workflow rows, in ledger order.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="hashPrefix">A recipe hash (in full or a prefix of 8 or more digits) to count only runs under it; null for every version.</param>
    /// <param name="window">The window.</param>
    /// <param name="now">The read's time.</param>
    /// <returns>The stats.</returns>
    public static RecipeStats Count(IReadOnlyList<StatEvent> events, RecipeId recipe, string? hashPrefix, StatsWindow window, DateTimeOffset now) =>
        throw new NotSupportedException("RecipeStatsCounter.Count is not built yet");

    /// <summary>Whether any root instance (an address of three segments) of <paramref name="recipe"/> ever started.</summary>
    /// <param name="events">The workflow rows.</param>
    /// <param name="recipe">The recipe.</param>
    /// <returns>True when one did.</returns>
    public static bool HasRoot(IReadOnlyList<StatEvent> events, RecipeId recipe) =>
        throw new NotSupportedException("RecipeStatsCounter.HasRoot is not built yet");
}
