using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.ShowRecipe;

/// <summary>
/// 'recipe show &lt;id&gt;[@&lt;hash&gt;]' and 'GET /workflow/recipes/{id}[@{hash}]' (API spec §3.8; query): the recipe as the API's <see cref="RecipeView"/>.
/// </summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Hash">The hash in full or a prefix of 8 or more digits; null for the approved version of <paramref name="Id"/>.</param>
public sealed record ShowRecipeRequest(RecipeId Id, string? Hash = null) : IQuery<RecipeView>;
