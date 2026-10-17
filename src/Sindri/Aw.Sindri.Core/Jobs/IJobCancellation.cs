using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The part of the job runtime CancelJob reaches (API P6): trips the work token of a job this process is running.</summary>
public interface IJobCancellation
{
    /// <summary>Trips the work token of a running job. The job keeps its (kind, key) claim until its work has returned.</summary>
    /// <param name="job">The job.</param>
    /// <returns>True when this process had the job running; false for a job it does not have (ended, or from before a restart).</returns>
    public bool Cancel(JobId job);

    /// <summary>Marks a job as being cancelled, before 'job.cancelled' is appended, so its work can see the cancel is coming before its token trips and record nothing of its own (D7).</summary>
    /// <param name="job">The job.</param>
    public void Cancelling(JobId job);

    /// <summary>Takes the mark back when the cancel could not be recorded.</summary>
    /// <param name="job">The job.</param>
    public void Withdraw(JobId job);

    /// <summary>Whether the cancel of <paramref name="job"/> that is under way ended up recorded, waiting for it to settle: a work that must not record its own end only when the cancel is on the ledger asks this.</summary>
    /// <param name="job">The job.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>True when 'job.cancelled' is recorded; false when no cancel is under way or it was withdrawn.</returns>
    public ValueTask<bool> CancelRecorded(JobId job, CancellationToken cancellationToken);

    /// <summary>Whether a cancel of <paramref name="job"/> has been requested or recorded.</summary>
    /// <param name="job">The job.</param>
    /// <returns>True when the job is being cancelled.</returns>
    public bool IsCancelling(JobId job);
}
