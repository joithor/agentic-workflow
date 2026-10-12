using Temporalio.Activities;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The spike's desk 'RunStep': records the desk task under '{address}#{run token}' and completes asynchronously.</summary>
/// <param name="desk">Where the task tokens go (the stand-in for the 'step.started' ledger row).</param>
internal sealed class SpikeDeskActivities(DeskTasks desk)
{
    /// <summary>Opens the desk task and returns pending.</summary>
    /// <param name="run">The run.</param>
    /// <returns>Never returns: 'complete --run' completes it through the task token.</returns>
    [Activity("SpikeRunStep")]
    public SpikeReport RunStep(SpikeStepRun run)
    {
        desk.Open(DeskTasks.Key(run.Address, run.Token), ActivityExecutionContext.Current.Info.TaskToken);
        throw new CompleteAsyncException();
    }
}
