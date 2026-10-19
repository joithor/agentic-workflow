namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One of the instance's events.</summary>
/// <param name="Sequence">The ledger sequence.</param>
/// <param name="Type">The event type.</param>
/// <param name="OccurredAt">When it was recorded.</param>
/// <param name="Transition">What its payload names; every field is optional.</param>
internal sealed record InstanceEventRow(long Sequence, string Type, DateTimeOffset OccurredAt, InstanceEventFields? Transition = null);
