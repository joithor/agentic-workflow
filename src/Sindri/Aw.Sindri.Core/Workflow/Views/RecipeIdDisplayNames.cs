using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The names until the engine supplies them: a recipe is shown by its id (the spec is silent on a recipe with no name, so the id is the honest fallback).</summary>
public sealed class RecipeIdDisplayNames : IRecipeDisplayNames
{
    /// <summary>The one instance, for a caller built without a lookup.</summary>
    public static RecipeIdDisplayNames Instance { get; } = new();

    /// <inheritdoc/>
    public string Of(RecipeId id) => id.Value;
}
