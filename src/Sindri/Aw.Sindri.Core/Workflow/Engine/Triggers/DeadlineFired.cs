namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// A deadline fired (spec §5.4, §5.6): valid only while it is armed and not paused. A state or retry deadline runs
/// the state's 'then'; a takeoverIdle deadline notifies the human once and clears (exempt from the approval check).
/// </summary>
/// <param name="Actor">'service:workflow.timers'.</param>
/// <param name="Deadline">The deadline, which is also the trigger id.</param>
public sealed record DeadlineFired(string Actor, DeadlineId Deadline) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => Deadline.Kind == DeadlineKind.TakeoverIdle ? TriggerNames.TakeoverIdle : TriggerNames.Deadline;

    /// <inheritdoc/>
    public override string KeyPart => Deadline.Value;

    /// <inheritdoc/>
    public override bool ExemptFromApproval => Deadline.Kind == DeadlineKind.TakeoverIdle;
}
