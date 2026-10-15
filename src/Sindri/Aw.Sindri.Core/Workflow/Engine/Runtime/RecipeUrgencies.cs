using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Where the engine reads a recipe's urgency (UI screens rev 8 E-21). Urgency is the validated string of
/// <see cref="Urgencies"/> until the API lane's urgency enum merges; then this one method parses it and nothing else
/// changes.
/// </summary>
public static class RecipeUrgencies
{
    /// <summary>The urgency of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The recipe an instance runs.</param>
    /// <returns>'incident', 'triage' or 'work'; <see cref="Urgencies.Default"/> when the recipe's isn't one of them.</returns>
    public static string UrgencyOf(InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Urgencies.IsKnown(recipe.Recipe.Urgency) ? recipe.Recipe.Urgency : Urgencies.Default;
    }
}
