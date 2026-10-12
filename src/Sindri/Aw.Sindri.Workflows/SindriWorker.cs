using Temporalio.Worker;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The worker 'sindri serve' runs on task queue 'sindri' (engine spec rev 8 §0 "Operations"): the one workflow type and
/// its activities. P0 contract: the registrations only; engine P4 hosts the worker inside 'sindri serve'.
/// </summary>
internal static class SindriWorker
{
    /// <summary>The worker options: <see cref="RecipeWorkflow"/> and the <see cref="RunStepActivities"/>.</summary>
    /// <param name="activities">The RunStep activities instance.</param>
    /// <returns>The options, on <see cref="SindriTemporal.TaskQueue"/>.</returns>
    public static TemporalWorkerOptions Options(RunStepActivities activities) =>
        new TemporalWorkerOptions(SindriTemporal.TaskQueue)
            .AddWorkflow<RecipeWorkflow>()
            .AddAllActivities(activities);
}
