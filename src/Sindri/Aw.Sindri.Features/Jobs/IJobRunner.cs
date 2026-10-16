namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The process-local runner of job work (API P6): the Host's JobRunner in production, the testing project's InlineJobRunner in unit tests.
/// It runs the work outside the command gate, queues its progress lines for a writer that appends 'job.progress' as 'service:jobs', and
/// appends the one terminal event ('job.finished' unless CancelJob already wrote 'job.cancelled'). It takes no heavy-job slot.
/// </summary>
public interface IJobRunner : Core.Jobs.IJobCancellation
{
    /// <summary>Jobs whose work is running now.</summary>
    public int Running { get; }

    /// <summary>Starts the work and returns once it is under way (the inline test runner returns when it ended).</summary>
    /// <param name="start">The accepted start.</param>
    /// <param name="cancellationToken">Cancels only the hand-over, never the work.</param>
    /// <returns>A task that completes when the work was started.</returns>
    public ValueTask Start(JobStart start, CancellationToken cancellationToken);
}
