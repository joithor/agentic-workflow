namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// A gate or evidence refused the matched transition (spec W14, §5.8): the trigger is consumed and what follows is
/// defined. Its workflow.refused event goes to the ledger; Temporal returns this outcome for a repeated Update id (§5.2).
/// </summary>
/// <param name="Key">The trigger's key.</param>
/// <param name="Outcome">What followed.</param>
/// <param name="Changes">The changed rows and their workflow.refused events: none for a command refused outright; a refused command on an answer's resume ('--reject', or '--accept' with another check refusing) ends the answer's suspension with its SND-WF-004 refusal (§5.8, W14).</param>
/// <param name="Effects">The effects to run (a park's or escalation's notification).</param>
/// <param name="FollowUps">The post-commit work (a re-run's start).</param>
public sealed record Refused(
    TriggerKey Key,
    RefusalOutcome Outcome,
    IReadOnlyList<InstanceChange> Changes,
    IReadOnlyList<EffectCommand> Effects,
    IReadOnlyList<FollowUp> FollowUps) : Decision(Key);
