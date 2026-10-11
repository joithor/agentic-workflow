namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A Step run failed (spec §3.5): a recipe transition on 'step.failed' takes it, otherwise the instance parks with 'needs-answer'.</summary>
/// <param name="Actor">Who reported it.</param>
/// <param name="Run">The run's token.</param>
/// <param name="Reason">What failed, in the words the human reads (scrubbed before it is recorded).</param>
/// <param name="Tokens">The tokens the run spent.</param>
/// <param name="ReportSha256">The SHA-256 of the canonical report.</param>
public sealed record StepFailed(string Actor, RunToken Run, string Reason, int Tokens, string ReportSha256) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.StepFailed;

    /// <inheritdoc/>
    public override string KeyPart => "run:" + Run.Value + ":" + ReportSha256;
}
