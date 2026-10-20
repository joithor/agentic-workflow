using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The names until the engine supplies them: none is known, so views serve no display name and the app makes the id readable itself (a raw id is never served as a name).</summary>
public sealed class NoRecipeDisplayNames : IRecipeDisplayNames
{
    /// <inheritdoc/>
    public string? Of(RecipeId id) => null;
}
