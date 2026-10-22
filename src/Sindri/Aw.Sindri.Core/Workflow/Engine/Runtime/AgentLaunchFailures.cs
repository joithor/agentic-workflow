using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Maps a <see cref="FailedLaunch"/> to what the engine does with the run (engine P8 D8).</summary>
public static class AgentLaunchFailures
{
    /// <summary>SND-LAUNCH-012 and -013 park the instance as a spec conflict; anything else ends the Step as 'step.failed' with the reason 'launch'.</summary>
    /// <param name="failed">The launcher's failure.</param>
    /// <returns>What to do.</returns>
    public static AgentLaunchFailure Of(FailedLaunch failed)
    {
        ArgumentNullException.ThrowIfNull(failed);
        var error = failed.Error;
        var parks = string.Equals(error.Code, SindriErrors.LaunchNotApproved.Code, StringComparison.Ordinal) || string.Equals(error.Code, SindriErrors.LaunchFormUnsupported.Code, StringComparison.Ordinal);
        return new AgentLaunchFailure(parks ? AgentLaunchFailureKind.Park : AgentLaunchFailureKind.StepFailed, error.Code, error.Message);
    }
}
