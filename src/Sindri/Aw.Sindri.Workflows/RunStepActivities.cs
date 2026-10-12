using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'RunStep': the activity an 'active' state starts (engine spec rev 8 §5.3). In the desk runtime it records
/// the desk task and returns pending (async completion); 'sindri workflow complete --run &lt;token&gt;' completes it
/// through its task token. P0 contract stub; engine P0 unit (b) implements the desk path.
/// </summary>
internal sealed class RunStepActivities
{
    /// <summary>Starts the Step and leaves the activity open for its report.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The report the human or the session completed it with.</returns>
    [Activity("RunStep")]
    public Task<StepReport> RunStepAsync(StepRun run) =>
        throw new NotSupportedException("engine P0 unit (b): record the desk task and complete asynchronously");
}
