namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Walks one babysitter instance's transitions, in ledger order, and adds up what <see cref="BabysitterRun"/> holds. The state names are <see cref="BabysitterNames"/>'s.</summary>
internal static class BabysitterRunWalker
{
    /// <summary>Walks the rows.</summary>
    /// <param name="rows">The instance's rows, the first the start.</param>
    /// <returns>What they add up to.</returns>
    public static BabysitterRun Walk(IReadOnlyList<StatEvent> rows)
    {
        var start = rows[0].At;
        return new BabysitterRun
        {
            ReadyAfter = After(rows, start, BabysitterNames.Ready),
            MergeAfter = After(rows, start, BabysitterNames.Merged),
            Tokens = rows.Max(static r => r.TokensSpent),
            Owner = Owner(rows),
            CiFix = CiFix(rows),
            Rechecks = Rechecks(rows),
            DocsWithMerge = Entries(rows, BabysitterNames.DocsMerging),
            DocsOwnerDesk = Entries(rows, BabysitterNames.DocsMergeOwner),
        };
    }

    private static double? After(IReadOnlyList<StatEvent> rows, DateTimeOffset start, string state) =>
        rows.FirstOrDefault(r => IsEntry(r, state)) is { } entered ? (entered.At - start).TotalSeconds : null;

    private static bool IsEntry(StatEvent row, string state) => string.Equals(row.To, state, StringComparison.Ordinal) && !string.Equals(row.From, state, StringComparison.Ordinal);

    private static int Entries(IReadOnlyList<StatEvent> rows, string state) => rows.Count(r => IsEntry(r, state));

    private static OwnerActionStats Owner(IReadOnlyList<StatEvent> rows)
    {
        int Left(string verb) => rows.Count(r => string.Equals(r.From, BabysitterNames.Ready, StringComparison.Ordinal) && string.Equals(r.To, BabysitterNames.OwnerVerbs[verb], StringComparison.Ordinal));
        return new OwnerActionStats(Left("merge"), Left("rework"), Left("comments"), Left("resync-docs"), Left("check"));
    }

    // an attempt is the entry into ci-fix; the next snapshot decides it: back to ci-fix is still red, anything else is fixed, none yet is open
    private static CiFixStats CiFix(IReadOnlyList<StatEvent> rows)
    {
        int attempts = 0, fixedOnes = 0, stillRed = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (!IsEntry(rows[i], BabysitterNames.CiFix))
            {
                continue;
            }

            attempts++;
            var next = rows.Skip(i + 1).FirstOrDefault(static r => string.Equals(r.From, BabysitterNames.Snapshot, StringComparison.Ordinal));
            if (next is null)
            {
                continue;
            }

            if (string.Equals(next.To, BabysitterNames.CiFix, StringComparison.Ordinal))
            {
                stillRed++;
            }
            else
            {
                fixedOnes++;
            }
        }

        return new CiFixStats(attempts, fixedOnes, stillRed, Rate: null);
    }

    private static RecheckStats Rechecks(IReadOnlyList<StatEvent> rows) => new(
        Entries(rows, BabysitterNames.Recheck),
        rows.Count(static r => string.Equals(r.From, BabysitterNames.Recheck, StringComparison.Ordinal) && string.Equals(r.To, BabysitterNames.Snapshot, StringComparison.Ordinal)),
        rows.Count(static r => string.Equals(r.From, BabysitterNames.Recheck, StringComparison.Ordinal) && string.Equals(r.To, BabysitterNames.RecheckOwner, StringComparison.Ordinal)));
}
