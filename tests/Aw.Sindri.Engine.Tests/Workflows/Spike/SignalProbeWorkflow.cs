using Temporalio.Workflows;
using Wf = Temporalio.Workflows.Workflow;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Signals (engine spec rev 8 §5.4): ends when the bumps reach the target, including bumps sent before the wait.</summary>
[Workflow("SignalProbe")]
internal sealed class SignalProbeWorkflow
{
    private int _sum;

    /// <summary>Waits for <paramref name="target"/>.</summary>
    /// <param name="target">The sum to reach.</param>
    /// <returns>The sum.</returns>
    [WorkflowRun]
    public async Task<int> RunAsync(int target)
    {
        await Wf.WaitConditionAsync(() => _sum >= target);
        return _sum;
    }

    /// <summary>Adds <paramref name="by"/>.</summary>
    /// <param name="by">The amount.</param>
    /// <returns>Done.</returns>
    [WorkflowSignal]
    public Task BumpAsync(int by)
    {
        _sum += by;
        return Task.CompletedTask;
    }
}
