using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>The engine-filled outputs of the open Step (DT spec §8.1) that an eval line leaves out, written as the engine does with provider "eval".</summary>
internal static class EvalFilledOutputs
{
    /// <summary>Adds the outputs the Step of <paramref name="at"/>'s state fills itself and the line omitted.</summary>
    /// <param name="outputs">The line's outputs.</param>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors.</param>
    /// <param name="at">The state the Step is open in, and the outcome the line reports.</param>
    /// <returns>The outputs, filled.</returns>
    public static Dictionary<FactKey, FactValue> Add(Dictionary<FactKey, FactValue> outputs, Recipe recipe, BlockCatalog catalog, (string State, OutcomeName Outcome) at)
    {
        if (!recipe.States.TryGetValue(at.State, out var state) || state.Step is not { } step || catalog.Find(step.Use) is not { } descriptor)
        {
            return outputs;
        }

        foreach (var (fact, text) in EngineFilledOutputs.For(descriptor.Outputs, at.Outcome, "eval", actor: null))
        {
            if (!outputs.ContainsKey(fact))
            {
                using var content = JsonDocument.Parse(text);
                outputs[fact] = EvalFacts.Value(content.RootElement);
            }
        }

        return outputs;
    }
}
