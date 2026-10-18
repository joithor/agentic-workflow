namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>One desk Step run: the instance, the state and its run token.</summary>
/// <param name="Address">The workflow id.</param>
/// <param name="State">The active state.</param>
/// <param name="Token">The run token '{entry}.{n}', also the activity id.</param>
internal sealed record SpikeStepRun(string Address, string State, string Token);
