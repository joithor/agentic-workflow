using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// SND-WF-016 (engine spec §5.1, E1): a recipe whose Step names an input this build has no runtime for is refused at
/// approve and at start, instead of its input being dropped without a word. Pure.
/// </summary>
public static class StepInputRefusals
{
    /// <summary>The first input (states in name order, inputs in declaration order) with no runtime.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="runtimes">The inputs this build runs.</param>
    /// <returns>SND-WF-016 naming 'states.&lt;s&gt;.step.inputs[k]' and the id, or null when every input runs.</returns>
    public static Error? Check(Recipe recipe, StepInputRuntimes runtimes)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(runtimes);
        return null;   // red stub (PR-A S2): refuses nothing yet
    }
}
