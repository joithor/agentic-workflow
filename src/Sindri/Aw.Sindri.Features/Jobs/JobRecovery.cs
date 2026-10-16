using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// Ends the jobs a previous process left running (API P6 unit F, decision D8: jobs are not durable). Once the views have caught up with the ledger's head
/// at the first attempt, every job still 'running' in them that this process does not run gets one 'job.finished' (failed, SND-JOB-001) as 'service:jobs'.
/// A job is never re-run, and an interrupted RunService writes no 'service.ran' (Q6). The end's key is the job's own end key, so a second recovery, or
/// a recovery after a job's real end, writes nothing new. The host calls <see cref="Run"/> until it succeeds.
/// </summary>
public sealed class JobRecovery
{
    private static readonly TimeSpan _catchUp = TimeSpan.FromSeconds(1);
    private readonly IJobViews _jobs;
    private readonly ILedgerReader _ledger;
    private readonly IProjectorStatus _status;
    private readonly HeldLease _held;
    private readonly JobEventWriter _writer;
    private readonly JobRegistry _registry;
    private long? _head;

    internal JobRecovery(IJobViews jobs, ILedgerReader ledger, IProjectorStatus status, HeldLease held, JobEventWriter writer, JobRegistry registry)
    {
        _jobs = jobs;
        _ledger = ledger;
        _status = status;
        _held = held;
        _writer = writer;
        _registry = registry;
    }

    /// <summary>One recovery attempt: needs the lease and views that include the head seen at the first attempt.</summary>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    /// <returns>How many jobs it ended, or why it could not yet (no lease, views behind or stalled, a failed write); the host tries again.</returns>
    public async ValueTask<Result<int>> Run(CancellationToken cancellationToken)
    {
        var head = await Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        if (_held.Current is null)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease");
        }

        var caught = await _status.WaitFor(head.Value, _catchUp, cancellationToken).ConfigureAwait(false);
        if (caught.IsError)
        {
            return caught.Error;
        }

        var running = await _jobs.Running(cancellationToken).ConfigureAwait(false);
        if (running.IsError)
        {
            return running.Error;
        }

        var ended = 0;
        foreach (var job in running.Value.Where(j => j.Sequence <= head.Value && !_registry.Holds(j.Id)))   // a job started after the first attempt is this process's
        {
            var written = await _writer.Interrupted(job, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }

            if (written.Value)
            {
                ended++;
            }
        }

        return ended;
    }

    private async ValueTask<Result<long>> Head(CancellationToken cancellationToken)
    {
        if (_head is { } known)
        {
            return known;
        }

        var head = await _ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsSuccess)
        {
            _head = head.Value;
        }

        return head;
    }
}
