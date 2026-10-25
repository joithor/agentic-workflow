using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How a recipe instance ended: the terminal state it reached and its outcome. Engine P4a.</summary>
/// <param name="State">The terminal state.</param>
/// <param name="Outcome">The terminal outcome ('resolved', or 'cancelled').</param>
/// <param name="Outputs">The recipe's declared outputs the instance held at its end, under its own keys (DT spec §8.4, engine P7 plan D4): what a parent's 'wait.satisfied' carries. Empty on 'cancelled'; null in a result recorded before P7.</param>
internal sealed record RecipeResult(string State, string? Outcome, IReadOnlyDictionary<FactKey, FactValue>? Outputs = null);
