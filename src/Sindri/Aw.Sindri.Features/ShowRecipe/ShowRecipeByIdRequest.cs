using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.ShowRecipe;

/// <summary>Show a recipe as the API's RecipeView by its id alone, resolving the approved hash (API spec section 3.8, GET /workflow/recipes/{id}; query).</summary>
/// <param name="Id">The recipe's id.</param>
public sealed record ShowRecipeByIdRequest(RecipeId Id) : IQuery<RecipeView>;
