using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What completes a Step: the declared outcome and the output facts (engine spec rev 8 §5.3). Engine P0 spike.</summary>
/// <param name="Outcome">One of the Step's declared outcomes.</param>
/// <param name="Outputs">The output facts by fact key: each a reference the completer stored before completion.</param>
internal sealed record StepReport(string Outcome, IReadOnlyDictionary<string, FactValue> Outputs);
