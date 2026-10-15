using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The recipe's inputs as the list row and the view show them (UI screens E-20): in declaration order, each required or present (v1 serves no optional input).</summary>
public static class InputSlots
{
    /// <summary>The slots of <paramref name="instance"/>'s recipe.</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="recipe">The recipe it runs and its input facts.</param>
    /// <returns>One slot per declared input.</returns>
    public static IReadOnlyList<InputSlot> Of(WorkflowInstance instance, InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        return
        [
            .. recipe.Recipe.Inputs.Select(i => new InputSlot(
                i.Key.Value,
                i.Label ?? SubjectRefs.Bare(i.Key.Value),
                i.Kind ?? i.Type ?? FactTypes.Payload,
                Required: true,
                Present: instance.Facts.ContainsKey(i.Key) || recipe.Inputs.ContainsKey(i.Key))),
        ];
    }
}
