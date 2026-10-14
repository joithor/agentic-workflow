namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// A park, escalation or takeover (spec §3.8, §5.1, §5.8): what the human reads, and what the answer that ends it
/// runs. The engine-owned state status shows ('parked', 'escalated', 'taken-over') is its kind.
/// </summary>
/// <param name="Kind">Park, escalation or takeover.</param>
/// <param name="Reason">A park's reason.</param>
/// <param name="ParkKind">Which §3.8 row a park is, where its reason has several.</param>
/// <param name="Escalation">An escalation's kind.</param>
/// <param name="Question">The question or detail ('{question}', '{reasons}', '{childNext}' …), scrubbed.</param>
/// <param name="Actor">Who caused it: the takeover's caller, or the trigger's actor for a park or escalation.</param>
/// <param name="Mirrors">The child it mirrors (§5.8): answers go to that child, and that child's own changes end it.</param>
/// <param name="Resume">What the answer (or the handback) that ends it runs.</param>
public sealed record Suspension(
    SuspensionKind Kind,
    ParkReason? Reason,
    ParkKind? ParkKind,
    EscalationKind? Escalation,
    string Question,
    string Actor,
    WorkflowInstanceId? Mirrors,
    ResumeAction Resume)
{
    /// <summary>The engine-owned state this suspension shows (spec §3.8).</summary>
    public string State => Kind switch
    {
        SuspensionKind.Escalation => EngineStates.Escalated,
        SuspensionKind.Takeover => EngineStates.TakenOver,
        SuspensionKind.Park or _ => EngineStates.Parked,
    };
}
