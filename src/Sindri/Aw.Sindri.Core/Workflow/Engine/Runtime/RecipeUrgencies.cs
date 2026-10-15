using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Where the engine reads a recipe's urgency (UI screens rev 8 E-21).</summary>
public static class RecipeUrgencies
{
    /// <summary>The urgency of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The recipe an instance runs.</param>
    /// <returns>The recipe's urgency; <see cref="RecipeUrgency.Work"/> when its word isn't one of <see cref="Urgencies.All"/>.</returns>
    public static RecipeUrgency UrgencyOf(InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Urgencies.IsKnown(recipe.Recipe.Urgency) ? Urgencies.Parse(recipe.Recipe.Urgency) : RecipeUrgency.Work;
    }
}
