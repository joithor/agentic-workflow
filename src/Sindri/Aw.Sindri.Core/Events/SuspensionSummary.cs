using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Events;

/// <summary>A suspension as a delta shows it: why, never the question.</summary>
/// <param name="Kind">Park, escalation or takeover.</param>
/// <param name="Reason">The park reason.</param>
/// <param name="ParkKind">The park's kind.</param>
/// <param name="Escalation">The escalation's kind.</param>
/// <param name="Mirrors">The child it mirrors.</param>
public sealed record SuspensionSummary(SuspensionKind Kind, ParkReason? Reason, ParkKind? ParkKind, EscalationKind? Escalation, string? Mirrors);
