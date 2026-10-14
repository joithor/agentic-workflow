namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>The trigger applied (spec §5.1, §5.6): one change per instance it changes (its own, plus a created child's), each with its one ledger event, and the effects and follow-ups the interpreter runs after them.</summary>
/// <param name="Key">The trigger's key.</param>
/// <param name="Changes">The changed rows and their events; the first is the trigger's own instance.</param>
/// <param name="Effects">The effects to run as activities (§5.7).</param>
/// <param name="FollowUps">The post-commit work.</param>
public sealed record Applied(
    TriggerKey Key,
    IReadOnlyList<InstanceChange> Changes,
    IReadOnlyList<EffectCommand> Effects,
    IReadOnlyList<FollowUp> FollowUps) : Decision(Key);
