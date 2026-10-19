namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What a failed launch does to the run (engine P8 D8).</summary>
public enum AgentLaunchFailureKind
{
    /// <summary>The launcher refused the entry or the Step's form (SND-LAUNCH-012, -013): the instance parks as a spec conflict, with the code.</summary>
    Park = 1,

    /// <summary>The host could not start the session: the Step ends as 'step.failed' with the reason 'launch'.</summary>
    StepFailed = 2,
}
