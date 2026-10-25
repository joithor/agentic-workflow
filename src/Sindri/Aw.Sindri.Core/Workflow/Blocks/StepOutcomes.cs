using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// The one resolver of a Step's outcomes (DT spec §8.1): every read of <see cref="BlockDescriptor.Outcomes"/> for a Step in a recipe goes through it.
/// The validator, the report validation, the eval and the desk's views (the open tasks and their forms) read through it.
/// </summary>
internal static class StepOutcomes
{
    /// <summary>The outcomes <paramref name="step"/> reports.</summary>
    /// <param name="descriptor">The Step's descriptor.</param>
    /// <param name="step">The Step as the recipe uses it.</param>
    /// <param name="recipe">The recipe, for a twin that reads another state's parameters.</param>
    /// <returns>The choice names in order, then the descriptor's fixed outcomes; the fixed ones alone without OutcomesFrom; null when the list is missing, not literal or doesn't parse.</returns>
    public static IReadOnlyList<OutcomeName>? Of(BlockDescriptor descriptor, StepUse step, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(recipe);
        if (descriptor.OutcomesFrom is not { } from)
        {
            return descriptor.Outcomes;
        }

        if (ParameterLists.Literal(from, step.With, recipe) is not { } list || ChoiceList.Parse(list) is not { Faults.Count: 0 } parsed)
        {
            return null;
        }

        return [.. parsed.Entries.Select(static c => c.Outcome).Concat(descriptor.Outcomes).Distinct()];
    }
}
