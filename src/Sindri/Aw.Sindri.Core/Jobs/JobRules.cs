using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The pure rules for jobs.</summary>
public static class JobRules
{
    /// <summary>Whether a job may be cancelled now.</summary>
    /// <param name="row">The job's row, or null when there is none (or the caller may not see it).</param>
    /// <returns>Success, SND-JOB-404 for no row, or SND-JOB-409 when the job already ended.</returns>
    public static Result<Unit> CanCancel(JobRow? row) => row switch
    {
        null => SindriErrors.JobNotFound.With("no such job"),
        { State: JobState.Running } => Unit.Value,
        _ => SindriErrors.JobConflict.With("the job already ended"),
    };

    /// <summary>The row a 'job.started' makes: running, no lines yet. A replay (the row exists) changes nothing.</summary>
    /// <param name="row">The row so far, or null before the job's first event.</param>
    /// <param name="started">The payload.</param>
    /// <param name="recorded">The recorded event (its time, actor and sequence).</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow? row, JobStarted started, LedgerEvent recorded) => throw new NotSupportedException("red until API P6 unit A");

    /// <summary>The row after a 'job.progress': the line is appended, the last 200 are kept and the rounds are the highest seen. A job that ended ignores it (D5).</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="progress">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobProgress progress, LedgerEvent recorded) => throw new NotSupportedException("red until API P6 unit A");

    /// <summary>The row after a 'job.finished': succeeded or failed, with its summary, code and result. A job that ended ignores it (D5: the first terminal event wins).</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="finished">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobFinished finished, LedgerEvent recorded) => throw new NotSupportedException("red until API P6 unit A");

    /// <summary>The row after a 'job.cancelled': cancelled, by whom. A job that ended ignores it (D5).</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="cancelled">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobCancelled cancelled, LedgerEvent recorded) => throw new NotSupportedException("red until API P6 unit A");
}
