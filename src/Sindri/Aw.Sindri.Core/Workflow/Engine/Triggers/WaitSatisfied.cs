using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>A wait state's 'wait.satisfied' (spec §3.5): its outcome comes from the source (the event's 'outcome' field, or the child's terminal outcome or 'cancelled').</summary>
/// <param name="Actor">The delivering service ('service:workflow.inbox').</param>
/// <param name="Source">What satisfied it.</param>
/// <param name="Outcome">The source's outcome, when it has one (a resource grant has none).</param>
/// <param name="Outputs">The facts the source records.</param>
public sealed record WaitSatisfied(
    string Actor,
    WaitSource Source,
    OutcomeName? Outcome,
    IReadOnlyDictionary<FactKey, FactValue> Outputs) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.WaitSatisfied;

    /// <inheritdoc/>
    public override string KeyPart => Source.KeyPart;
}
