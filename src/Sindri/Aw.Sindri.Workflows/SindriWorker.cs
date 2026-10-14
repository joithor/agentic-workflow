using Temporalio.Worker;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The worker 'sindri serve' runs on task queue 'sindri' (engine spec rev 8 §0 "Operations"): the one workflow type and
/// its activities. Engine P4a: <see cref="SindriWorkerHost"/> runs it inside 'sindri serve'.
/// </summary>
internal static class SindriWorker
{
    /// <summary>The worker options: <see cref="RecipeWorkflow"/>, the <see cref="RunStepActivities"/>, 'RecordEvent', 'ReadItemRepo' and the effect activities.</summary>
    /// <param name="activities">The RunStep activities instance.</param>
    /// <param name="events">The RecordEvent activity instance (the interpreter runs it as a local activity).</param>
    /// <param name="items">The ReadItemRepo activity instance (a local activity on start).</param>
    /// <param name="effects">The effect activities: RecordEffect, RecheckEffect, RunEffect (engine P6).</param>
    /// <returns>The options, on <see cref="SindriTemporal.TaskQueue"/>.</returns>
    public static TemporalWorkerOptions Options(RunStepActivities activities, RecordEventActivities events, ItemFactActivities items, EffectActivities effects) =>
        new TemporalWorkerOptions(SindriTemporal.TaskQueue)
            .AddWorkflow<RecipeWorkflow>()
            .AddAllActivities(activities)
            .AddAllActivities(events)
            .AddAllActivities(items)
            .AddAllActivities(effects);
}
