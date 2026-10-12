using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// Approve a registered recipe hash (engine spec §4.2, §6.3 'recipe approve', POST /workflow/recipes/{id}@{hash}/approve;
/// protected). The answer must redeem an 'approve &lt;id&gt; &lt;version&gt;' challenge on this hash; re-approving an approved
/// hash whose pins drifted is a drift approval.
/// </summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash to approve.</param>
/// <param name="Answer">The challenge and the text the human typed.</param>
public sealed record ApproveRecipeRequest(RecipeId Id, RecipeHash Hash, ChallengeAnswer Answer) : ICommand<ApproveRecipeResponse>;
