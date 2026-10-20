using Aw.Kernel;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.ApproveAllRecipes;

/// <summary>
/// The checks an approve-all start makes inside the gate, in the spec's order (setup-once spec §5.3): the profile is approved (SND-SETUP-007), no approve-all job runs
/// (SND-JOB-409 with 'runningJob'), then the challenge is redeemed against the plan as it is now (SND-RECIPE-009 when the plan changed). Redeeming is last, so a refusal that is
/// not the owner's does not burn the hold; a failed claim after it is a clean refusal that burns the challenge. Nothing is written here.
/// </summary>
/// <param name="plans">Reads the plan, refusing before a profile is approved.</param>
/// <param name="challenges">The server's challenges.</param>
/// <param name="registry">The running jobs.</param>
/// <param name="redeemed">Keeps the redeemed plan for the job.</param>
internal sealed class ApproveAllRecipesAdmission(ApproveAllPlanReader plans, IChallenges challenges, IRunningJobs registry, RedeemedPlans redeemed) : IJobAdmission<ApproveAllRecipesRequest>
{
    /// <summary>The job's kind: the action id.</summary>
    internal static readonly JobKind Kind = JobKind.From("ApproveAllRecipes");

    /// <summary>The job's fixed key: one approve-all at a time.</summary>
    internal static readonly JobKey Key = JobKey.From("approve-all");

    public async ValueTask<Result<ServiceName?>> Admit(ApproveAllRecipesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = await plans.Read(cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (registry.IsRunning(Kind, Key, out var running))
        {
            return JobConflicts.Running(Kind, Key, running);
        }

        var taken = RedeemedPlan.Redeem(challenges, request.Answer, plan.Value);
        if (taken.IsError)
        {
            return taken.Error;
        }

        redeemed.Keep(request, taken.Value);
        return (ServiceName?)null;
    }

    public void Rollback(ApproveAllRecipesRequest request) => redeemed.Drop(request);
}
