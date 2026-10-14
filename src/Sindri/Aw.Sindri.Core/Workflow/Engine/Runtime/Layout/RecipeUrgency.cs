namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>
/// Where the engine reads a recipe's urgency (UI screens rev 8 E-21). The recipe model P4b builds on has no
/// 'urgency' field yet (engine P2b adds it), so every recipe is <see cref="Urgency.Work"/> until that merges; then
/// this one method reads the field and nothing else changes.
/// </summary>
public static class RecipeUrgency
{
    /// <summary>The urgency of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The recipe an instance runs.</param>
    /// <returns><see cref="Urgency.Work"/> until P2b's field exists.</returns>
    public static Urgency Of(InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Urgency.Work;
    }
}
