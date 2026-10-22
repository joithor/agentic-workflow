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
        var addresses = Instances(events, window, now);
        var byAddress = events.GroupBy(static e => e.Address, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => (IReadOnlyList<StatEvent>)[.. g], StringComparer.Ordinal);
        var runs = addresses.Select(a => BabysitterRunWalker.Walk(byAddress[a])).ToList();
        var tally = new BabysitterRecordTally();
        foreach (var address in addresses.Where(records.ContainsKey))
        {
            tally.Add(records[address]);
        }

        var fix = new CiFixStats(runs.Sum(static r => r.CiFix.Attempts), runs.Sum(static r => r.CiFix.Succeeded), runs.Sum(static r => r.CiFix.StillRed), Rate: null);
        return new BabysitterStats(
            runs.Count,
            Spread(runs.Select(static r => r.ReadyAfter)),
            Spread(runs.Select(static r => r.MergeAfter)),
            Spread(runs.Select(static r => (double?)r.Tokens)),
            tally.Comments(),
            fix with { Rate = fix.Succeeded + fix.StillRed == 0 ? null : (double)fix.Succeeded / (fix.Succeeded + fix.StillRed) },
            new RecheckStats(runs.Sum(static r => r.Rechecks.Runs), runs.Sum(static r => r.Rechecks.Resolved), runs.Sum(static r => r.Rechecks.Unresolved)),
            new OwnerActionStats(runs.Sum(static r => r.Owner.Merge), runs.Sum(static r => r.Owner.Rework), runs.Sum(static r => r.Owner.Comments), runs.Sum(static r => r.Owner.ResyncDocs), runs.Sum(static r => r.Owner.Check)),
            new BabysitterDocsStats(runs.Sum(static r => r.DocsWithMerge), runs.Sum(static r => r.DocsOwnerDesk), tally.DocsOutcomes()),
            tally.Errors);
    }

    private static BabysitterSpread Spread(IEnumerable<double?> values)
    {
        var sorted = values.OfType<double>().Order().ToList();
        return new BabysitterSpread(StatsMath.Median(sorted), StatsMath.Rank(sorted, 0.9));
    }
}
