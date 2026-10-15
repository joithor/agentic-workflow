namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Who produced a fact and when: what a fact row's 'by' and 'at' show (UI screens E-20).</summary>
/// <param name="By">The actor string of the trigger that wrote the fact ('human:thor', 'step.produce', 'service:workflow'; README X61).</param>
/// <param name="At">When the instance recorded it (the workflow's deterministic clock).</param>
public sealed record FactProvenance(string By, DateTimeOffset At);
