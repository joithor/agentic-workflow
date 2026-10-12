namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>A refusal as 'workflow.refused' records it (spec §5.6): what followed.</summary>
/// <param name="Follow">What followed.</param>
/// <param name="Park">The park's reason, when it parked.</param>
/// <param name="Escalation">The escalation's kind, when it escalated.</param>
/// <param name="Round">The round the re-run starts, when it re-ran.</param>
/// <param name="Code">The error code a refused command got (SND-WF-004).</param>
public sealed record RefusalRecord(RefusalFollow Follow, ParkReason? Park, EscalationKind? Escalation, int? Round, string? Code);
