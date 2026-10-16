using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What decides the answer a suspension takes.</summary>
/// <param name="Kind">The suspension kind.</param>
/// <param name="Reason">The park reason.</param>
/// <param name="ParkKind">What the park is about.</param>
/// <param name="Escalation">The escalation kind.</param>
public sealed record ParkShape(SuspensionKind Kind, ParkReason? Reason, ParkKind? ParkKind, EscalationKind? Escalation);
