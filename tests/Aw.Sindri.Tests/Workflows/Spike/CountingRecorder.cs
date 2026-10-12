using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Activities;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Registers the real 'RecordEvent' under its own name and counts how often the worker executed it.</summary>
/// <param name="inner">The real activity.</param>
internal sealed class CountingRecorder(RecordEventActivities inner)
{
    private readonly Lock _gate = new();
    private readonly List<string> _log = [];

    /// <summary>Each execution: '{activity id}:{new|again}', in order (a re-execution shows as 'again').</summary>
    public IReadOnlyList<string> Log
    {
        get
        {
            lock (_gate)
            {
                return [.. _log];
            }
        }
    }

    /// <summary>How many times a worker executed RecordEvent.</summary>
    public int Executions => Log.Count;

    /// <summary>Counts, then runs the real activity.</summary>
    /// <param name="record">The event.</param>
    /// <returns>The ledger append.</returns>
    [Activity("RecordEvent")]
    public async Task<LedgerAppend> RecordAsync(WorkflowEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var appended = await inner.RecordAsync(record);
        lock (_gate)
        {
            _log.Add($"{ActivityExecutionContext.Current.Info.ActivityId}:{(appended.AlreadyRecorded ? "again" : "new")}");
        }

        return appended;
    }
}
