namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>The trigger applied (spec W13): one Commit with one change per instance (the instance's own, plus a created child's), its outbox commands and its follow-ups.</summary>
/// <param name="Key">The trigger's key.</param>
/// <param name="Changes">The changed rows and their events; the first is the trigger's own instance.</param>
/// <param name="Outbox">The outbox commands.</param>
/// <param name="FollowUps">The post-commit work.</param>
public sealed record Applied(
    TriggerKey Key,
    IReadOnlyList<InstanceChange> Changes,
    IReadOnlyList<EffectCommand> Outbox,
    IReadOnlyList<FollowUp> FollowUps) : Decision(Key);
