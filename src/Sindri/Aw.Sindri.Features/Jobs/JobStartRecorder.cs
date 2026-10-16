using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.Jobs;

/// <summary>Appends 'job.started' for the start service (API P6 unit C), fenced by the held lease, as the starting caller.</summary>
/// <param name="held">The lease the start is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class JobStartRecorder(HeldLease held, ILedgerStore ledger, EventFactory events)
{
    /// <summary>Records the start of <paramref name="job"/>.</summary>
    /// <param name="job">The new job.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="key">Its key.</param>
    /// <param name="caller">The starting caller (the event carries its actor and correlation).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The grant it wrote under, the sequence of 'job.started' and its envelope id; or why it could not be written.</returns>
    public async ValueTask<Result<RecordedStart>> Record(JobId job, JobKind kind, JobKey key, CallerContext caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var started = new JobStarted(caller.CommandId, job, kind, key, Item: null, caller.Repo, Service: null);
        var entry = events.Entry(EventTypes.JobStarted, started, $"{EventTypes.JobStarted}:{job.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new RecordedStart(grant, appended.Value.Sequence, EventId.From(entry.Envelope.Id));
    }
}
