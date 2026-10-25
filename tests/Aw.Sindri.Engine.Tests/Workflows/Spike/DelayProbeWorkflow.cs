using Temporalio.Workflows;
using Wf = Temporalio.Workflows.Workflow;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>A durable timer (engine spec rev 8 §5.5): Wf.DelayAsync for the given time.</summary>
[Workflow("DelayProbe")]
internal sealed class DelayProbeWorkflow
{
    /// <summary>True once the run has begun.</summary>
    [WorkflowQuery]
    public bool Started { get; private set; }

    /// <summary>Sleeps <paramref name="delay"/> of workflow time.</summary>
    /// <param name="delay">The delay.</param>
    /// <returns>The workflow time that passed.</returns>
    [WorkflowRun]
    public async Task<TimeSpan> RunAsync(TimeSpan delay)
    {
        Started = true;
        var start = Wf.UtcNow;
        await Wf.DelayAsync(delay);
        return Wf.UtcNow - start;
    }
}
