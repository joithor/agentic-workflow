using Temporalio.Activities;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The probes' activity: records its task token and completes asynchronously.</summary>
/// <param name="desk">Where the task tokens go.</param>
internal sealed class ProbeActivities(DeskTasks desk)
{
    /// <summary>Opens a desk task and returns pending; a client completes it through the task token.</summary>
    /// <param name="label">What the probe passes through.</param>
    /// <returns>Never returns: the activity completes asynchronously.</returns>
    [Activity("ProbeDesk")]
    public string Desk(string label)
    {
        _ = label;
        var context = ActivityExecutionContext.Current;
        desk.Open(context.Info.ActivityId, context.Info.TaskToken);
        throw new CompleteAsyncException();
    }
}
