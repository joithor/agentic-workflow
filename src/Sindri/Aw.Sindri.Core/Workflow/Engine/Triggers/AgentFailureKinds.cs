namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>The values of <see cref="StepFailed.FailureKind"/> for an agent Step (model-pick spec 7.1): why the run failed, so an escalation and the model history can tell them apart.</summary>
public static class AgentFailureKinds
{
    /// <summary>The report was refused (ReportRefusals took its text).</summary>
    public const string Refused = "refused";

    /// <summary>The report was read and the Step failed its check.</summary>
    public const string CheckFailed = "check_failed";

    /// <summary>The session ended with no report: an exit with no refusal, lost, idle, or its launch failed.</summary>
    public const string Crashed = "crashed";
}
