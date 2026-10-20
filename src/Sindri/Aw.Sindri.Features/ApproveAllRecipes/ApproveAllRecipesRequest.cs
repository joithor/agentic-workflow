using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>
/// Approve every shipped recipe of the plan with one confirmation (setup-once spec §5.3, §7.2, POST /workflow/recipes/approve-all; protected): a job with the
/// fixed key 'approve-all', so a second start while one runs is SND-JOB-409 with 'runningJob'. The answer must redeem 'approve-all &lt;digest&gt;' on the plan as it is
/// now; it is redeemed last, after the checks that read only, so a refusal that is not the owner's does not burn the confirmation.
/// </summary>
/// <param name="Answer">The challenge and the text the human typed.</param>
public sealed record ApproveAllRecipesRequest(ChallengeAnswer Answer) : IJob<ApproveAllResult>;
