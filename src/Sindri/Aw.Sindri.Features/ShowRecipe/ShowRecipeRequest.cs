using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ShowRecipe;

/// <summary>Show a registered recipe's preview (engine spec §6.3 'recipe show &lt;id@hash&gt;', GET /workflow/recipes/{id}@{hash}; query).</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">Its hash, or a prefix of 8 or more hex digits (RecipeHash.IsPrefix).</param>
public sealed record ShowRecipeRequest(RecipeId Id, string Hash) : IQuery<ShowRecipeResponse>;
