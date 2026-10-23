using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The words for a recipe's states: the recipe's displayName for a state, else the state's id made readable. One lookup for every place that
/// shows a state to a person (the run canvas, the Workflows list, the board's cards), so none of them shows a raw id when the recipe has a name for it.
/// The recipes are read once each and kept: a state whose recipe is not read yet (or never is) reads as its id made readable.
/// </summary>
internal sealed class StateNames
{
    private readonly Dictionary<string, Dictionary<string, string>> _byRecipe = new(StringComparer.Ordinal);
    private readonly HashSet<string> _asked = new(StringComparer.Ordinal);

    /// <summary>The words for a state of a recipe.</summary>
    /// <param name="recipe">The recipe, or null when it is not read.</param>
    /// <param name="state">The state's id.</param>
    /// <returns>The recipe's displayName for the state, else its id made readable.</returns>
    internal static string Of(RecipeView? recipe, string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var served = recipe?.States.FirstOrDefault(s => string.Equals(s.State, state, StringComparison.Ordinal))?.DisplayName;
        return !string.IsNullOrWhiteSpace(served) ? served : WorkflowNames.Of(state, displayName: null);
    }

    /// <summary>The words for a state of a recipe this lookup has read.</summary>
    /// <param name="recipeId">The recipe's id.</param>
    /// <param name="state">The state's id.</param>
    /// <returns>The recipe's displayName for the state, else its id made readable.</returns>
    internal string Of(string recipeId, string state) =>
        _byRecipe.TryGetValue(recipeId, out var names) && names.TryGetValue(state, out var served) ? served : WorkflowNames.Of(state, displayName: null);

    /// <summary>True when the recipe has been asked for already (read or not): it is not asked for again.</summary>
    /// <param name="recipeId">The recipe's id.</param>
    /// <returns>True when it was asked for.</returns>
    internal bool WasAsked(string recipeId) => _asked.Contains(recipeId);

    /// <summary>Notes that the recipe is being asked for.</summary>
    /// <param name="recipeId">The recipe's id.</param>
    internal void Asked(string recipeId) => _ = _asked.Add(recipeId);

    /// <summary>Keeps the names a recipe serves.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>True when it served any name for a state.</returns>
    internal bool Add(RecipeView recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var named = recipe.States.Where(s => !string.IsNullOrWhiteSpace(s.DisplayName)).GroupBy(s => s.State, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().DisplayName!, StringComparer.Ordinal);   // why: the filter keeps only states with a name
        _byRecipe[recipe.Id] = named;
        return named.Count > 0;
    }
}
