namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// Classifies one repo for the map publish gate (code index spec 6.4). The rows apply in this order, which makes the table total:
/// Built, Absent, Broken, Backoff, then Coming as the default.
/// </summary>
public static class IndexOutlookRules
{
    /// <summary>Failed jobs in a row after which the index counts as broken (the code index service's alarm uses the same number).</summary>
    public const int FailuresToBroken = 3;

    /// <summary>Classifies.</summary>
    /// <param name="input">What is known of the repo.</param>
    /// <param name="now">The time.</param>
    /// <returns>The outlook, the owner's word and the retry time.</returns>
    public static IndexOutlookDetail Classify(IndexOutlookInput input, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.Equals(input.IndexCommit, input.HoldHead, StringComparison.Ordinal))
        {
            return new IndexOutlookDetail(IndexOutlook.Built, RepoWait.None, RetryAt: null);
        }

        if (input.NeverBuilt || !input.InScope)
        {
            return new IndexOutlookDetail(IndexOutlook.Absent, RepoWait.NotIndexed, RetryAt: null);
        }

        if (!input.ServiceActive || !input.HoldOnDefaultBranch)
        {
            return new IndexOutlookDetail(IndexOutlook.Absent, RepoWait.None, RetryAt: null);
        }

        var failures = input.FinishedNewestFirst.TakeWhile(static a => a.Failed).Count();
        return failures >= FailuresToBroken
            ? new IndexOutlookDetail(IndexOutlook.Broken, RepoWait.None, RetryAt: null)
            : NotBuilt(input, failures, now);
    }

    // Backoff while a failed job's retry is not due; otherwise Coming, worded by what is going on.
    private static IndexOutlookDetail NotBuilt(IndexOutlookInput input, int failures, DateTimeOffset now)
    {
        if (input.BuildRunning)
        {
            return Coming(RepoWait.Building);
        }

        var finished = input.FinishedNewestFirst;
        if (failures > 0)
        {
            return Backoff(RepoWait.Retrying, finished[0].EndedAt + SyncGapDecision.Wait(failures), now);
        }

        if (input.IndexCommit is null)
        {
            return Coming(RepoWait.Damaged);
        }

        var moved = input.LiveHead is not null && !string.Equals(input.LiveHead, input.HoldHead, StringComparison.Ordinal);
        return Coming(moved ? RepoWait.NewerIndex : RepoWait.Queued);
    }

    private static IndexOutlookDetail Backoff(RepoWait word, DateTimeOffset retryAt, DateTimeOffset now) =>
        now < retryAt ? new IndexOutlookDetail(IndexOutlook.Backoff, word, retryAt) : Coming(RepoWait.Queued);

    private static IndexOutlookDetail Coming(RepoWait word) => new(IndexOutlook.Coming, word, RetryAt: null);
}
