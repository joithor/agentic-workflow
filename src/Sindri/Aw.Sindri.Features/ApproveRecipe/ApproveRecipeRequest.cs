using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// Approve a registered recipe hash (engine spec §4.2, §6.3 'recipe approve', POST /workflow/recipes/{id}@{hash}/approve;
/// protected). No typed challenge is asked for (owner decision q93): the approval runs the validator and the evals first; re-approving an approved
/// hash whose pins drifted is a drift approval.
/// </summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash to approve.</param>
/// <param name="Answer">Accepted and ignored, so an older client still works.</param>
public sealed record ApproveRecipeRequest(RecipeId Id, RecipeHash Hash, ChallengeAnswer? Answer = null) : ICommand<ApproveRecipeResponse>;
