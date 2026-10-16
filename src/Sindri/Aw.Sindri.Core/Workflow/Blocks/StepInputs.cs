using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>The one resolver of a Step's inputs (DT spec §8.1).</summary>
public static class StepInputs
{
    /// <summary>The facts a Step reads.</summary>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <param name="with">The Step's 'with:' values.</param>
    /// <param name="recipe">The recipe, for a twin that reads another state's parameters.</param>
    /// <returns>The listed facts, then the descriptor's fixed inputs; null when the list is missing, not literal or doesn't parse.</returns>
    public static IReadOnlyList<FactKey>? Of(BlockDescriptor descriptor, IReadOnlyDictionary<string, WithValue> with, Recipe recipe) =>
        throw new NotSupportedException("StepInputs.Of is not implemented yet");
}
