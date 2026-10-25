using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Closing an agent run (engine P8 D11): the session stops when its run is over, and the interpreter loop stops one a park or a takeover kept when the instance ends or the run is replaced.</summary>
internal sealed partial class RecipeWorkflow
{
    // A run that was cancelled or replaced closes: its session is stopped. A park or a takeover keeps the session (D11).
    private async Task<WorkflowInstance> StopIfClosed(WorkflowInstance instance, AgentRunning running)
    {
        if (instance.Ended || (instance.Suspension is null && instance.Frame.Run?.Token != running.Open.Token))
        {
            _ = await Close(running, measure: false).ConfigureAwait(true);
        }

        return instance;
    }

    // The loop's end-of-pass check (D11, F2): the session a park or a takeover kept is stopped when the instance ends or an answer moves it to another run.
    private async Task DropEndedAgent(WorkflowInstance instance)
    {
        if (_held is { } held && (instance.Ended || (instance.Suspension is null && instance.Frame.Run?.Token != held.Open.Token)))
        {
            _ = await Close(held, measure: false).ConfigureAwait(true);
        }
    }

    private async Task<AgentClosed> Close(AgentRunning running, bool measure)
    {
        _held = null;   // closed here, so the loop doesn't close it again
        var close = new AgentCloseRun(AgentLaunches.LaunchIdFor(running.Run).Value, running.Launched?.Worktree, running.Launched?.StartHead, measure);
        try
        {
            return await Workflow.ExecuteActivityAsync((AgentStepActivities a) => a.CloseAsync(close), AgentOptions(_closeCall, running.Open, running.State, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (ActivityFailureException)
        {
            return new AgentClosed(Tokens: 0, EmptyDiff: false);   // boundary: a close that can't read the usage must not wedge the instance; the run is decided with 0 tokens
        }
    }

    private sealed record AgentRunning(OpenRun Open, RecipeState State, AgentStepRun Run, AgentLaunched? Launched);
}
