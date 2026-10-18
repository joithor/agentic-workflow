using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>Runs the test interpreter's worker for the duration of a body.</summary>
internal static class SpikeWorker
{
    /// <summary>The worker options: the interpreter, the desk Step and the counting RecordEvent.</summary>
    /// <param name="queue">The task queue.</param>
    /// <param name="desk">The desk tasks.</param>
    /// <param name="ledger">The ledger.</param>
    /// <param name="maxCachedWorkflows">The sticky cache size; 0 replays every workflow task from the start.</param>
    /// <returns>The options.</returns>
    public static TemporalWorkerOptions Options(string queue, DeskTasks desk, SpikeLedger ledger, int maxCachedWorkflows = 1000) =>
        new TemporalWorkerOptions(queue) { MaxCachedWorkflows = maxCachedWorkflows }
            .AddWorkflow<SpikeStepWorkflow>()
            .AddAllActivities(new SpikeDeskActivities(desk))
            .AddAllActivities(ledger.Recorder);

    /// <summary>Runs <paramref name="body"/> while a worker with <paramref name="options"/> polls.</summary>
    /// <param name="client">The client.</param>
    /// <param name="options">The worker options.</param>
    /// <param name="body">The body.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The body's task.</returns>
    public static async Task Run(ITemporalClient client, TemporalWorkerOptions options, Func<Task> body, CancellationToken cancellationToken)
    {
        using var worker = new TemporalWorker(client, options);
        await worker.ExecuteAsync(body, cancellationToken);
    }
}
