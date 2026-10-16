using Aw.Kernel;

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
}
