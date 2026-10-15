using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'RecordEvent' (engine spec rev 8.1 §5.6): mirrors one interpreter event into the ledger, fenced by the held lease and
/// idempotent on 'workflow:{workflow id}:{run id}:{activity id}'. Every Temporal activity, local or regular, has a
/// deterministic ActivityInfo.ActivityId, the same on every replay; no activity sees a history event id, so the id the ledger records (column 'temporal_event_id', UI E-30) is that activity id. The interpreter
/// runs it as a <b>local</b> activity, so it costs one marker per row: a replay reads the marker and never executes it
/// again, and the one case that runs it twice (a worker that dies after the append but before the workflow task
/// completes) has the same activity id, so S1's ledger refuses the repeated key and records nothing. Failures throw, so
/// Temporal retries without bound and the workflow waits for the ledger.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="events">The control plane's event factory (its scope, clock, ids and the host's JSON options).</param>
internal sealed class RecordEventActivities(ILedgerStore ledger, HeldLease lease, EventFactory events)
{
    /// <summary>
    /// How the interpreter runs it: a local activity with unbounded retries and backoff, so the workflow does not move on
    /// until the ledger has the event (engine spec rev 8 §5.6).
    /// </summary>
    public static LocalActivityOptions LocalOptions { get; } = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(10),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromMilliseconds(100),
            BackoffCoefficient = 2,
            MaximumInterval = TimeSpan.FromSeconds(30),
            MaximumAttempts = 0,
        },
    };

    /// <summary>The ledger idempotency key of the RecordEvent execution <paramref name="info"/> describes.</summary>
    /// <param name="info">The activity's info: its workflow id, run id and deterministic activity id.</param>
    /// <returns>'workflow:{workflow id}:{run id}:{activity id}'.</returns>
    public static string KeyOf(ActivityInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return $"workflow:{info.WorkflowId}:{info.WorkflowRunId}:{info.ActivityId}";
    }

    /// <summary>Appends <paramref name="record"/> under the key of this execution (<see cref="KeyOf"/>).</summary>
    /// <param name="record">The event.</param>
    /// <returns>Its ledger position, and whether it was already there.</returns>
    [Activity("RecordEvent")]
    public async Task<LedgerAppend> RecordAsync(WorkflowEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var grant = lease.Current ?? throw new InvalidOperationException("SND-LEASE-002: this process holds no lease; the event waits for one");
        var activity = ActivityExecutionContext.Current;
        var payload = new WorkflowEventPayload(record.WorkflowId, record.From, record.To, record.Trigger, record.Outcome, record.Run);
        var own = events.Scope;
        var scope = new EventScope(record.Actor ?? own.Actor, record.Tenant ?? own.Tenant, record.Ring ?? own.Ring);
        var entry = events.Entry(record.Type, payload, KeyOf(activity.Info), record.Item, scope) with { TemporalEvent = activity.Info.ActivityId };   // the key's third part (UI E-30)
        var appended = await ledger.Append(entry, grant, activity.CancellationToken).ConfigureAwait(false);
        return appended.IsError
            ? throw new InvalidOperationException($"{appended.Error.Code}: {appended.Error.Message}")
            : appended.Value;
    }
}
