namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A child's park, resume or escalation, delivered to the parent through the inbox (spec §5.7); its id is the child event's key.</summary>
/// <param name="Actor">'service:workflow.inbox'.</param>
/// <param name="Child">The child.</param>
/// <param name="Change">What changed.</param>
/// <param name="Reason">The child's park reason, for <see cref="ChildChange.Parked"/>.</param>
/// <param name="Question">The child's question or next text, which the parent's mirror shows ('{childNext}').</param>
/// <param name="ChildEventKey">The key of the child's event.</param>
public sealed record ChildChanged(
    string Actor,
    WorkflowInstanceId Child,
    ChildChange Change,
    ParkReason? Reason,
    string? Question,
    TriggerKey ChildEventKey) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => Change switch
    {
        ChildChange.Parked => TriggerNames.ChildParked,
        ChildChange.Resumed => TriggerNames.ChildResumed,
        ChildChange.Escalated or _ => TriggerNames.ChildEscalated,
    };

    /// <inheritdoc/>
    public override string KeyPart => "child:" + ChildEventKey.Value;
}
