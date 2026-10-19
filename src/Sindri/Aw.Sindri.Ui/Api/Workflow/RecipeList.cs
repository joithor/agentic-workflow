namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /sindri/workflow/recipes: every registered recipe, whatever its standing.</summary>
/// <param name="Recipes">The rows in the API's order.</param>
internal sealed record RecipeList(IReadOnlyList<RecipeSummaryView> Recipes);
