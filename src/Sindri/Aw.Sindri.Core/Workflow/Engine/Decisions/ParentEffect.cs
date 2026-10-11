namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>A child change's effect on the parent (spec §5.7).</summary>
/// <param name="Kind">What it does.</param>
/// <param name="Suspend">The mirrored park or the child escalation, for <see cref="ParentEffectKind.MirrorPark"/> and <see cref="ParentEffectKind.Escalate"/>.</param>
public sealed record ParentEffect(ParentEffectKind Kind, SuspendOutcome? Suspend);
