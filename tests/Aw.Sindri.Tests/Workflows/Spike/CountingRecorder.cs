using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Activities;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Registers the real 'RecordEvent' under its own name and counts how often the worker executed it.</summary>
/// <param name="inner">The real activity.</param>
internal sealed class CountingRecorder(RecordEventActivities inner)
{
    private int _executions;

    /// <summary>How many times a worker executed RecordEvent (a replay that re-executed it would raise this).</summary>
    public int Executions => Volatile.Read(ref _executions);

    /// <summary>Counts, then runs the real activity.</summary>
    /// <param name="record">The event.</param>
    /// <returns>The ledger append.</returns>
    [Activity("RecordEvent")]
    public Task<LedgerAppend> RecordAsync(WorkflowEventRecord record)
    {
        _ = Interlocked.Increment(ref _executions);
        return inner.RecordAsync(record);
    }
}
