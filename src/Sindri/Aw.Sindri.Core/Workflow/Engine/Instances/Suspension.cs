namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// What suspended a frame, with what the human reads (spec §3.8, §5.1): the engine-owned state's 'next' text is
/// rendered from it.
/// </summary>
/// <param name="Kind">Park, escalation or takeover.</param>
/// <param name="Reason">A park's reason.</param>
/// <param name="ParkKind">Which §3.8 row a park is, where its reason has several.</param>
/// <param name="Escalation">An escalation's kind.</param>
/// <param name="Question">The question or detail ('{question}', '{reasons}', '{childNext}' …), scrubbed.</param>
/// <param name="Actor">Who caused it: the takeover's caller, or the trigger's actor for a park or escalation.</param>
/// <param name="Mirrors">The child this suspension mirrors; such a frame is popped by that child's id, not from the top (§5.1).</param>
public sealed record Suspension(
    SuspensionKind Kind,
    ParkReason? Reason,
    ParkKind? ParkKind,
    EscalationKind? Escalation,
    string Question,
    string Actor,
    WorkflowInstanceId? Mirrors);
