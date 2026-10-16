using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The part of the job runtime CancelJob reaches (API P6): trips the work token of a job this process is running.</summary>
public interface IJobCancellation
{
    /// <summary>Trips the work token of a running job. The job keeps its (kind, key) claim until its work has returned.</summary>
    /// <param name="job">The job.</param>
    /// <returns>True when this process had the job running; false for a job it does not have (ended, or from before a restart).</returns>
    public bool Cancel(JobId job);
}
