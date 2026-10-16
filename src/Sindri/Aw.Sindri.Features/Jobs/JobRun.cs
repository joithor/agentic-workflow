using Aw.Kernel;

namespace Aw.Sindri.Features.Jobs;

/// <summary>One running job as the runner tracks it: its start, its work's token source, its cancel state and the first write failure.</summary>
/// <param name="start">The accepted start.</param>
/// <param name="source">Trips the work token.</param>
internal sealed class JobRun(JobStart start, CancellationTokenSource source)
{
    private volatile bool _cancelled;
    private volatile bool _recorded;

    public JobStart Start { get; } = start;

    public CancellationTokenSource Source { get; } = source;

    public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A cancel was requested (the mark) or recorded; the runner's Withdraw takes the mark back when the record failed.</summary>
    public bool Cancelled { get => _cancelled; set => _cancelled = value; }

    /// <summary>'job.cancelled' is in the ledger.</summary>
    public bool Recorded { get => _recorded; set => _recorded = value; }

    /// <summary>Completes (true: recorded, false: withdrawn) when the cancel in flight is settled; null when none was requested.</summary>
    public TaskCompletionSource<bool>? Pending { get; set; }

    public Error? WriteFailure { get; set; }
}
