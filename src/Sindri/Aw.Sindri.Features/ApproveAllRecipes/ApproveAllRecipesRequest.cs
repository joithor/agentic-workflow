using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>
/// Approve every shipped recipe of the plan with one call (setup-once spec §5.3, §7.2, POST /workflow/recipes/approve-all; protected): a job with the
/// fixed key 'approve-all', so a second start while one runs is SND-JOB-409 with 'runningJob'. No typed challenge is asked for (owner decision q93).
/// </summary>
/// <param name="Answer">Accepted and ignored, so an older client still works.</param>
public sealed record ApproveAllRecipesRequest(ChallengeAnswer? Answer = null) : IJob<ApproveAllResult>;
