using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// The name the app shows for a recipe instead of its id (setup-once spec §7.7): the recipe's required 'displayName'. The workflow engine implements it
/// from the registered recipes; every view that names a recipe asks it, so a client never shows 'workflow.step'.
/// </summary>
public interface IRecipeDisplayNames
{
    /// <summary>The display name of <paramref name="id"/>.</summary>
    /// <param name="id">The recipe's id.</param>
    /// <returns>The name; the id made readable (<see cref="RecipeIdDisplayNames.Readable"/>) when no name is known (a recipe from before the field, or one not registered). Never empty.</returns>
    public string Of(RecipeId id);
}
