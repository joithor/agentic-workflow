namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>Where a desk report left the instance (engine spec rev 8 §6.3: '-> checks'). Engine P4a.</summary>
/// <param name="State">The state the instance is in after the report; null when the report wasn't waited on.</param>
/// <param name="Run">The run token now open, if any.</param>
public sealed record DeskOutcome(string? State, RunToken? Run);
