using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.RevokeRecipe;

/// <summary>
/// Revoke an approved recipe hash (engine spec §4.2, §6.3 'recipe revoke', POST /workflow/recipes/{id}@{hash}/revoke;
/// protected). The answer must redeem a 'revoke &lt;id&gt;@&lt;hash12&gt;' challenge. The revoke cascades to the generated
/// recipes approved from it and wakes the running instances so they park at once.
/// </summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash to revoke.</param>
/// <param name="Answer">The challenge and the text the human typed.</param>
public sealed record RevokeRecipeRequest(RecipeId Id, RecipeHash Hash, ChallengeAnswer Answer) : ICommand<RevokeRecipeResponse>;
