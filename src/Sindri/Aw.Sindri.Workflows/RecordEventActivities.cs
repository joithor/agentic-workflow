using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Temporalio.Activities;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'RecordEvent' (engine spec rev 8 §5.6): mirrors one interpreter event into the ledger. P0 (c) red stub.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds; every append is fenced by it.</param>
/// <param name="scope">Who the process acts as.</param>
/// <param name="clock">The clock.</param>
/// <param name="ids">The envelope id source.</param>
internal sealed class RecordEventActivities(ILedgerStore ledger, HeldLease lease, EventScope scope, TimeProvider clock, IIdGenerator ids)
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

    private readonly object _ports = (ledger, lease, scope, clock, ids, WorkflowsJsonContext.Default);

    /// <summary>Appends <paramref name="record"/> under its idempotency key.</summary>
    /// <param name="record">The event.</param>
    /// <returns>Its ledger position, and whether it was already there.</returns>
    [Activity("RecordEvent")]
    public Task<LedgerAppend> RecordAsync(WorkflowEventRecord record) =>
        throw new NotSupportedException($"red: P0 (c) mirrors {record.Type} into the ledger ({_ports})");
}
