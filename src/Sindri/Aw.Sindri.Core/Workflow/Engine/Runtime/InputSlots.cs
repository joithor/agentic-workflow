using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The recipe's inputs as the list row and the view show them (UI screens E-20): in declaration order. A slot's kind is the input's subject adapter; it is required unless the input has a generate template.</summary>
public static class InputSlots
{
    /// <summary>The slots of <paramref name="instance"/>'s recipe.</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="recipe">The recipe it runs and its input facts.</param>
    /// <returns>One slot per declared input.</returns>
    public static IReadOnlyList<InputPresence> Of(WorkflowInstance instance, InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        return
        [
            .. recipe.Recipe.Inputs.Select(i => new InputPresence(
                i.Key.Value,
                i.Label ?? SubjectRefs.Bare(i.Key.Value),
                SubjectKinds.Effective(i),   // the subject adapter, as RecipeView.inputs[].kind serves it (API spec §3.8)
                Required: i.Generate is null,
                Present: instance.Facts.ContainsKey(i.Key) || recipe.Inputs.ContainsKey(i.Key))),
        ];
    }
}
