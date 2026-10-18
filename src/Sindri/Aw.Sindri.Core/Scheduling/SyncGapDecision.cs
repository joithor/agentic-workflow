namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// The pure decision of the 'context-sync-gaps' service (scoping spec C5): a gap, a running job, then the backoff after failures in a row (5, 10, 20, 40, then 60 minutes at most,
/// counted from the newest failure; any other outcome resets it). The service re-checks on the scheduler tick by reading the jobs view; the scheduler has no event trigger.
/// </summary>
public static class SyncGapDecision
{
    private static readonly TimeSpan _first = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _longest = TimeSpan.FromMinutes(60);

    /// <summary>Decides.</summary>
    /// <param name="gap">Whether a configured intent source has never been tried.</param>
    /// <param name="running">Whether a sync job is running.</param>
    /// <param name="finishedNewestFirst">The ended sync jobs, newest first.</param>
    /// <param name="now">The time.</param>
    /// <returns>The action.</returns>
    public static SyncGapAction Decide(bool gap, bool running, IReadOnlyList<SyncAttempt> finishedNewestFirst, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(finishedNewestFirst);
        if (!gap)
        {
            return SyncGapAction.Nothing;
        }

        if (running)
        {
            return SyncGapAction.JobRunning;
        }

        var failures = finishedNewestFirst.TakeWhile(static a => a.Failed).Count();
        return failures > 0 && now < finishedNewestFirst[0].EndedAt + Wait(failures) ? SyncGapAction.BackoffNotDue : SyncGapAction.Start;
    }

    /// <summary>The wait after <paramref name="failuresInARow"/> failures.</summary>
    /// <param name="failuresInARow">One or more.</param>
    /// <returns>5, 10, 20, 40, then 60 minutes.</returns>
    public static TimeSpan Wait(int failuresInARow) => TimeSpan.FromTicks(Math.Min(_longest.Ticks, _first.Ticks << Math.Min(Math.Max(failuresInARow - 1, 0), 8)));
}
