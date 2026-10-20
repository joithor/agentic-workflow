namespace Aw.Sindri.Features.SetupWatch;

/// <summary>What <see cref="SetupJobQueue.Submit"/> did with a job.</summary>
internal enum SetupJobOutcome
{
    /// <summary>The job was handed to the runner.</summary>
    Started,

    /// <summary>The slot is busy or the job limit is reached: the job waits its turn.</summary>
    Queued,

    /// <summary>The same job already waits or runs: nothing was added.</summary>
    Duplicate,
}
