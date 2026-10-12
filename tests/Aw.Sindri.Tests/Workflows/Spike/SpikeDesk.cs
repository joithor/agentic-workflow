using Temporalio.Client;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// The desk side of the spike: what 'sindri workflow complete --run &lt;token&gt;' does (map the run token to the task
/// token and complete the activity), and waiting for the instance to settle after a report.
/// </summary>
/// <param name="client">The client.</param>
/// <param name="desk">The desk tasks.</param>
internal sealed class SpikeDesk(ITemporalClient client, DeskTasks desk)
{
    /// <summary>Starts the interpreter for <paramref name="run"/> as workflow <paramref name="id"/>.</summary>
    /// <param name="id">The workflow id (the instance address).</param>
    /// <param name="queue">The task queue.</param>
    /// <param name="run">The input.</param>
    /// <returns>The handle.</returns>
    public Task<WorkflowHandle<SpikeStepWorkflow, SpikeView>> Start(string id, string queue, SpikeRun run) =>
        client.StartWorkflowAsync((SpikeStepWorkflow w) => w.RunAsync(run), new WorkflowOptions(id, queue));

    /// <summary>Completes run <paramref name="token"/> of <paramref name="id"/> with <paramref name="report"/>.</summary>
    /// <param name="id">The workflow id.</param>
    /// <param name="token">The run token.</param>
    /// <param name="report">The report.</param>
    /// <param name="cancellationToken">Cancels the wait for the task.</param>
    /// <returns>Done.</returns>
    public async Task Complete(string id, string token, SpikeReport report, CancellationToken cancellationToken)
    {
        var taskToken = await desk.WaitFor(DeskTasks.Key(id, token), cancellationToken);
        await client.GetAsyncActivityHandle(taskToken).CompleteAsync(report);
    }

    /// <summary>Sends 'complete' for run <paramref name="token"/> as an Update with the nonce as its id.</summary>
    /// <param name="handle">The instance.</param>
    /// <param name="token">The run token.</param>
    /// <param name="report">The report.</param>
    /// <param name="nonce">The update id.</param>
    /// <returns>The run token the instance completed.</returns>
    public static Task<string> CompleteByUpdate(WorkflowHandle<SpikeStepWorkflow, SpikeView> handle, string token, SpikeReport report, string nonce) =>
        handle.ExecuteUpdateAsync(w => w.CompleteAsync(token, report), new WorkflowUpdateOptions(nonce));

    /// <summary>Waits until the run token <paramref name="token"/> of <paramref name="id"/> has opened its desk task.</summary>
    /// <param name="id">The workflow id.</param>
    /// <param name="token">The run token.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <param name="within">How long to wait (20 s by default) before a TimeoutException.</param>
    /// <returns>Done.</returns>
    public Task Opened(string id, string token, CancellationToken cancellationToken, TimeSpan? within = null) =>
        desk.WaitFor(DeskTasks.Key(id, token), cancellationToken, within);
}
