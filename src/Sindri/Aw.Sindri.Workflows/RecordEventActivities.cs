using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'RecordEvent' (engine spec rev 8 §5.6): mirrors one interpreter event into the ledger, fenced by the held lease and
/// idempotent on '(workflow id, run id, sequence)'. The interpreter runs it as a <b>local</b> activity: its result is a
/// marker in the same workflow task, so a replay reads the marker and never executes it again; the one case that runs it
/// twice (a worker that dies after the append but before the workflow task completes) hits the idempotency key, and S1's
/// append records nothing. Failures throw, so Temporal retries without bound and the workflow waits for the ledger.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="scope">Who the process acts as.</param>
/// <param name="clock">The clock.</param>
/// <param name="ids">The envelope id source.</param>
internal sealed class RecordEventActivities(ILedgerStore ledger, HeldLease lease, EventScope scope, TimeProvider clock, IIdGenerator ids)
{
    private readonly EventFactory _events = new(scope, clock, ids, AwJson.CreateOptions(WorkflowsJsonContext.Default));

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

    /// <summary>Appends <paramref name="record"/> under its idempotency key.</summary>
    /// <param name="record">The event.</param>
    /// <returns>Its ledger position, and whether it was already there.</returns>
    [Activity("RecordEvent")]
    public async Task<LedgerAppend> RecordAsync(WorkflowEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var grant = lease.Current ?? throw new InvalidOperationException("SND-LEASE-002: this process holds no lease; the event waits for one");
        var payload = new WorkflowEventPayload(record.WorkflowId, record.From, record.To, record.Trigger, record.Outcome, record.Run);
        var entry = _events.Entry(record.Type, payload, record.IdempotencyKey) with { Item = record.Item };
        var appended = await ledger.Append(entry, grant, ActivityExecutionContext.HasCurrent ? ActivityExecutionContext.Current.CancellationToken : CancellationToken.None).ConfigureAwait(false);
        return appended.IsError
            ? throw new InvalidOperationException($"{appended.Error.Code}: {appended.Error.Message}")
            : appended.Value;
    }
}
