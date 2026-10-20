using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// The name the app shows for a recipe instead of its id (setup-once spec §7.7): the recipe's 'displayName', required on a recipe file once the engine lands RCP-DISPLAY-001.
/// The workflow engine implements it from the registered recipes; every view that names a recipe asks it.
/// </summary>
public interface IRecipeDisplayNames
{
    /// <summary>The display name of <paramref name="id"/>.</summary>
    /// <param name="id">The recipe's id.</param>
    /// <returns>The name, or null when none is known (a recipe from before the field, or one not registered); the view then serves none and the app makes the id readable.</returns>
    public string? Of(RecipeId id);
}
