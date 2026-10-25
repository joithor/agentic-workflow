using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What decides the answer a suspension takes.</summary>
/// <param name="Kind">The suspension kind.</param>
/// <param name="Reason">The park reason.</param>
/// <param name="ParkKind">What the park is about.</param>
/// <param name="Escalation">The escalation kind.</param>
/// <param name="Mirrors">Whether the suspension mirrors a child, whose own fit the answer is forwarded to.</param>
/// <param name="MirroredParkKind">For a mirror: what the child's own park is about.</param>
/// <param name="MirroredEscalation">For a mirror: the child's own escalation kind.</param>
public sealed record ParkShape(
    SuspensionKind Kind,
    ParkReason? Reason,
    ParkKind? ParkKind,
    EscalationKind? Escalation,
    bool Mirrors = false,
    ParkKind? MirroredParkKind = null,
    EscalationKind? MirroredEscalation = null)
{
    /// <summary>The shape of a suspension.</summary>
    /// <param name="suspension">The park or escalation.</param>
    /// <returns>Its shape.</returns>
    public static ParkShape Of(Suspension suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        return new(suspension.Kind, suspension.Reason, suspension.ParkKind, suspension.Escalation, suspension.Mirrors is not null, suspension.MirroredParkKind, suspension.MirroredEscalation);
    }
}
