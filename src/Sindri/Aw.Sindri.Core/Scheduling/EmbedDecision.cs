namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// The pure decision of the 'code-index-embed' service for one repo (code index spec §6.3): an embed job is started whenever the repo's embeddings layer is pending, with no HEAD move needed.
/// A running job first (never doubled), then nothing when the layer is not pending, then the wait after the newest ended job: the 5, 10, 20, 40, then 60 minutes of <see cref="SyncGapDecision"/>
/// after failures in a row, <see cref="TryLaterWait"/> after a job that found the index busy or missing (not a failure), <see cref="NoProgressWait"/> after a job that left the layer
/// pending without more vectors (so a stuck layer does not make a busy loop), and none after a job that embedded something.
/// </summary>
public static class EmbedDecision
{
    /// <summary>The wait after a job that found a build holding the repo's index, or no index yet.</summary>
    public static TimeSpan TryLaterWait { get; } = TimeSpan.FromMinutes(2);

    /// <summary>The wait after a job that ended with the layer still pending and no progress.</summary>
    public static TimeSpan NoProgressWait { get; } = TimeSpan.FromMinutes(10);

    /// <summary>Decides.</summary>
    /// <param name="pending">Whether the repo's embeddings layer is pending.</param>
    /// <param name="running">Whether an embed job of the repo is running.</param>
    /// <param name="finishedNewestFirst">The repo's ended embed jobs, newest first.</param>
    /// <param name="now">The time.</param>
    /// <returns>The action.</returns>
    public static MainMoveAction Decide(bool pending, bool running, IReadOnlyList<EmbedAttempt> finishedNewestFirst, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(finishedNewestFirst);
        if (running)
        {
            return MainMoveAction.JobRunning;
        }

        if (!pending)
        {
            return MainMoveAction.Nothing;
        }

        return finishedNewestFirst is [var newest, ..] && now < newest.EndedAt + Wait(newest.Kind, FailedInARow(finishedNewestFirst))
            ? MainMoveAction.BackoffNotDue
            : MainMoveAction.Start;
    }

    /// <summary>The failed jobs at the head of the list, up to the first one that did not fail.</summary>
    /// <param name="finishedNewestFirst">The ended jobs, newest first.</param>
    /// <returns>The count.</returns>
    public static int FailedInARow(IReadOnlyList<EmbedAttempt> finishedNewestFirst)
    {
        ArgumentNullException.ThrowIfNull(finishedNewestFirst);
        return finishedNewestFirst.TakeWhile(static a => a.Kind == EmbedAttemptKind.Failed).Count();
    }

    private static TimeSpan Wait(EmbedAttemptKind kind, int failures) => kind switch
    {
        EmbedAttemptKind.Failed => SyncGapDecision.Wait(failures),
        EmbedAttemptKind.TryLater => TryLaterWait,
        EmbedAttemptKind.NoProgress => NoProgressWait,
        EmbedAttemptKind.Progress => TimeSpan.Zero,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
    };
}
