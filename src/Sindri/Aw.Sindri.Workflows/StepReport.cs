namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What completes a Step: the declared outcome and the output facts (engine spec rev 8 §5.3). P0 contract stub.</summary>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">The output facts by name: each a ref the API stored before completion.</param>
internal sealed record StepReport(string Outcome, IReadOnlyDictionary<string, string> Outputs);
