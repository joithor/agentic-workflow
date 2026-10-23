namespace Aw.CodeIndex.Core.Planning;

/// <summary>
/// Whether a repo's pending layers continue, or its next start waits (spec §6.3), as a pure function; the main-move service calls it before the shared decision on every tick.
/// </summary>
public static class IndexContinuationDecision
{
    /// <summary>The wait after a continuation that embedded something.</summary>
    public static readonly TimeSpan Continue = TimeSpan.FromMinutes(2);

    /// <summary>The hold after a deferred job, or one that embedded nothing.</summary>
    public static readonly TimeSpan Hold = TimeSpan.FromMinutes(10);

    /// <summary>Continuations in a row that embedded nothing, after which none starts until the head moves.</summary>
    public const int GiveUpAfter = 3;

    /// <summary>Decides.</summary>
    /// <param name="head">The checkout's head on the default branch.</param>
    /// <param name="recordedHead">The commit the stored index describes, or null.</param>
    /// <param name="newestFirst">The repo's ended build jobs, newest first.</param>
    /// <param name="now">The time.</param>
    /// <returns>The action.</returns>
    public static ContinuationAction Decide(string? head, string? recordedHead, IReadOnlyList<IndexJobOutcome> newestFirst, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newestFirst);
        if (newestFirst.Count == 0)
        {
            return ContinuationAction.Pass;
        }

        var newest = newestFirst[0];
        if (newest.Deferred && now < newest.EndedAt + Hold)
        {
            return ContinuationAction.Suppress;
        }

        // A failed or deferred job published nothing: the continuation is read from the newest job that did, and the shared decision's backoff paces the retry.
        var published = newestFirst.TakeWhile(static j => j.Failed || j.Deferred).Count();
        if (published == newestFirst.Count || !newestFirst[published].LayersPending)
        {
            return ContinuationAction.Pass;
        }

        var moved = recordedHead is null || !string.Equals(head, recordedHead, StringComparison.Ordinal);
        if (published > 0)
        {
            return moved ? ContinuationAction.Pass : ContinuationAction.WithholdRecorded;
        }

        return Continuing(newestFirst, moved, now);
    }

    // The newest job left a layer pending. A moved head starts on its own, unless the hold applies to it too.
    private static ContinuationAction Continuing(IReadOnlyList<IndexJobOutcome> newestFirst, bool moved, DateTimeOffset now)
    {
        var newest = newestFirst[0];
        if (newestFirst[0].Embedded > Before(newestFirst, 0))
        {
            return moved ? ContinuationAction.Pass : After(newest, Continue, now);
        }

        if (EmptyInARow(newestFirst) >= GiveUpAfter)
        {
            return moved ? ContinuationAction.Pass : ContinuationAction.Suppress;
        }

        if (now < newest.EndedAt + Hold)
        {
            return ContinuationAction.Suppress;
        }

        return moved ? ContinuationAction.Pass : ContinuationAction.WithholdRecorded;
    }

    private static ContinuationAction After(IndexJobOutcome job, TimeSpan wait, DateTimeOffset now) =>
        now < job.EndedAt + wait ? ContinuationAction.Suppress : ContinuationAction.WithholdRecorded;

    private static int EmptyInARow(IReadOnlyList<IndexJobOutcome> newestFirst)
    {
        var count = 0;
        for (var i = 0; i < newestFirst.Count; i++)
        {
            var job = newestFirst[i];
            if (job.Failed || job.Deferred || !job.LayersPending || job.Embedded > Before(newestFirst, i))
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static int Before(IReadOnlyList<IndexJobOutcome> newestFirst, int index)
    {
        for (var i = index + 1; i < newestFirst.Count; i++)
        {
            if (!newestFirst[i].Failed && !newestFirst[i].Deferred)
            {
                return string.Equals(newestFirst[i].Commit, newestFirst[index].Commit, StringComparison.Ordinal) ? newestFirst[i].Embedded : 0;
            }
        }

        return 0;
    }
}
