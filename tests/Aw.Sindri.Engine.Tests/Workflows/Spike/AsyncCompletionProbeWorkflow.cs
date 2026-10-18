using Temporalio.Workflows;
using Wf = Temporalio.Workflows.Workflow;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>An async-completion activity (engine spec rev 8 §5.3) whose activity id is the run token '1.1'.</summary>
[Workflow("AsyncCompletionProbe")]
internal sealed class AsyncCompletionProbeWorkflow
{
    /// <summary>True once the run has begun.</summary>
    [WorkflowQuery]
    public bool Started { get; private set; }

    /// <summary>Runs the desk activity and returns what completed it.</summary>
    /// <returns>The completion value.</returns>
    [WorkflowRun]
    public Task<string> RunAsync()
    {
        Started = true;
        return Wf.ExecuteActivityAsync((ProbeActivities a) => a.Desk("probe"),
            new ActivityOptions { ActivityId = "1.1", ScheduleToCloseTimeout = TimeSpan.FromHours(1) });
    }
}
