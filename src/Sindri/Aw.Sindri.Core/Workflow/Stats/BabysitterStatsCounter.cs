using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Counts the babysitter's figures (PR babysitter spec API19) from the ledger's transitions and the instances' merge records.</summary>
public static class BabysitterStatsCounter
{
    /// <summary>The addresses of the babysitter instances started inside the window, in the order they first appear.</summary>
    /// <param name="events">The workflow ledger rows.</param>
    /// <param name="window">The stats window.</param>
    /// <param name="now">The time the window ends at.</param>
    /// <returns>The addresses.</returns>
    public static IReadOnlyList<string> Instances(IReadOnlyList<StatEvent> events, StatsWindow window, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(window);
        var since = now - window.Span;
        return [.. events.Where(e => e.RecipeId is { } id && string.Equals(id.Value, BabysitterNames.Recipe, StringComparison.Ordinal) && e.At >= since)
            .Select(static e => e.Address).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Counts the figures.</summary>
    /// <param name="events">The workflow ledger rows.</param>
    /// <param name="records">Each instance's merge record, by address; an instance with no entry has an empty one.</param>
    /// <param name="window">The stats window.</param>
    /// <param name="now">The time the window ends at.</param>
    /// <returns>The figures.</returns>
    public static BabysitterStats Count(IReadOnlyList<StatEvent> events, IReadOnlyDictionary<string, MergeRecordRows> records, StatsWindow window, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(window);
        throw new NotSupportedException("not counted yet: " + now.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
