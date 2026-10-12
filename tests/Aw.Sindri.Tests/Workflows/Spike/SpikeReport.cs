namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>What completes a desk Step: the outcome and the output facts.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Outputs">The output facts by key.</param>
internal sealed record SpikeReport(string Outcome, IReadOnlyDictionary<string, string> Outputs);
