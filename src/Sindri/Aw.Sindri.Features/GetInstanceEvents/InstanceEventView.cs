namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>One of an instance's ledger events, with the transition its payload names.</summary>
/// <param name="Sequence">The ledger sequence.</param>
/// <param name="Type">The event type ('workflow.started', 'workflow.transition', …).</param>
/// <param name="OccurredAt">When it was recorded.</param>
/// <param name="Actor">Who it was recorded for.</param>
/// <param name="Transition">The state change, trigger, outcome and run its payload names; empty fields when it names none.</param>
public sealed record InstanceEventView(long Sequence, string Type, DateTimeOffset OccurredAt, string Actor, InstanceEventFields Transition);
