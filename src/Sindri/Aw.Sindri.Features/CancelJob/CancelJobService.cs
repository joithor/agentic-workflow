using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.CancelJob;

/// <summary>
/// Cancels a job (API P6 unit E; job class, waits for the views): <see cref="JobRules.CanCancel"/> on the job's row, then 'job.cancelled' keyed by the
/// job, then the work's token trips. The first terminal event wins in the fold, so a cancel racing the job's own end shows one state. A running row
/// with no live runner (left by a process that died) is ended by the same append.
/// </summary>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller: the command id and who cancels.</param>
/// <param name="runtime">Trips the work token of a job this process runs.</param>
internal sealed class CancelJobService(IJobViews jobs, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers, IJobCancellation runtime)
    : IService<CancelJobRequest, CommandAccepted>
{
    public async ValueTask<Result<CommandAccepted>> Handle(CancelJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("CancelJob needs a caller");
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var row = await jobs.Get(request.Job, cancellationToken).ConfigureAwait(false);
        if (row.IsError)
        {
            return row.Error;
        }

        var allowed = JobRules.CanCancel(row.Value);
        if (allowed.IsError)
        {
            return allowed.Error;
        }

        runtime.Cancelling(request.Job);   // before the append, so the work sees it coming and records nothing of its own
        var entry = events.Entry(EventTypes.JobCancelled, new JobCancelled(caller.CommandId, request.Job, caller.Actor, row.Value?.Item, row.Value?.Repo), $"{EventTypes.JobCancelled}:{request.Job.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            runtime.Withdraw(request.Job);
            return appended.Error;
        }

        _ = runtime.Cancel(request.Job);
        return new CommandAccepted(caller.CommandId, appended.Value.Sequence);
    }
}
