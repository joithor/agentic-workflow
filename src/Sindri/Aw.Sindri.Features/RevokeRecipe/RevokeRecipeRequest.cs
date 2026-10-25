using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.RevokeRecipe;

/// <summary>
/// Revoke an approved recipe hash (engine spec §4.2, §6.3 'recipe revoke', POST /workflow/recipes/{id}@{hash}/revoke;
/// protected). No typed challenge is asked for (owner decision q93): The revoke cascades to the generated
/// recipes approved from it and wakes the running instances so they park at once.
/// </summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash to revoke.</param>
/// <param name="Answer">Accepted and ignored, so an older client still works.</param>
public sealed record RevokeRecipeRequest(RecipeId Id, RecipeHash Hash, ChallengeAnswer? Answer = null) : ICommand<RevokeRecipeResponse>;
