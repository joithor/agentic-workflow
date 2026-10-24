using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// Approve a registered recipe hash (engine spec §4.2, §6.3 'recipe approve', POST /workflow/recipes/{id}@{hash}/approve). A human's
/// answer must redeem an 'approve &lt;id&gt; &lt;version&gt;' challenge on this hash. An agent (a session or a service) sends no
/// challenge: its approval is accepted when the recipe's eval files, carried in <paramref name="Evals"/>, pass against this
/// hash (owner q93, 2026-10-23). Re-approving an approved hash whose pins drifted is a drift approval.
/// </summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Hash">The hash to approve.</param>
/// <param name="Answer">The challenge and the text the human typed; null for an agent.</param>
/// <param name="Evals">The eval files the recipe lists, for an agent; none needed when the recipe lists none.</param>
public sealed record ApproveRecipeRequest(RecipeId Id, RecipeHash Hash, ChallengeAnswer? Answer = null, IReadOnlyList<EvalFile>? Evals = null) : ICommand<ApproveRecipeResponse>;
