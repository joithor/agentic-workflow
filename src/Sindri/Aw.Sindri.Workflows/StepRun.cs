namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One run of an 'active' state's Step (engine spec rev 8.1 §5.3): what 'RunStep' records as the open task.</summary>
/// <param name="Address">The instance address.</param>
/// <param name="State">The state whose Step runs.</param>
/// <param name="Token">The run token '{entry}.{n}', also in the activity id.</param>
internal sealed record StepRun(string Address, string State, string Token);
