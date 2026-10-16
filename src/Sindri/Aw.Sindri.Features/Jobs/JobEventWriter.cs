using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.Jobs;

/// <summary>Appends a job's own events ('job.progress', 'job.finished') as 'service:jobs', fenced by the held lease, with the job's start event as their causation (API P6 unit C).</summary>
internal sealed class JobEventWriter
{
    /// <summary>Makes the writer.</summary>
    /// <param name="ledger">Appends the events.</param>
    /// <param name="held">The lease every write is fenced by.</param>
    /// <param name="events">Builds the entries.</param>
    /// <param name="scrubber">Cleans lines, summaries and results.</param>
    /// <param name="callers">Where 'service:jobs' is entered.</param>
    public JobEventWriter(ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers)
    {
        _ = (ledger, held, events, scrubber, callers);
    }

    /// <summary>Records one progress line (cleaned here) as round <paramref name="round"/>.</summary>
    /// <param name="start">The job.</param>
    /// <param name="round">Its number.</param>
    /// <param name="line">The raw line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the line was not recorded (a lost lease is SND-LEASE-003).</returns>
    public ValueTask<Result<Unit>> Progress(JobStart start, int round, string line, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit C");

    /// <summary>Records how the job ended.</summary>
    /// <param name="start">The job.</param>
    /// <param name="work">How its work ended.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the end was not recorded.</returns>
    public ValueTask<Result<Unit>> Finish(JobStart start, JobWorkResult work, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit C");
}
