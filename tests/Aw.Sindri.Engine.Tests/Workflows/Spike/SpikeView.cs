namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>What the interpreter's query and result show: the state, the park reason, the open run token and the entry.</summary>
/// <param name="State">The state, or 'parked'.</param>
/// <param name="Park">The park reason, when parked.</param>
/// <param name="Run">The last run token.</param>
/// <param name="Entry">The state-entry counter.</param>
internal sealed record SpikeView(string State, string? Park, string? Run, int Entry);
