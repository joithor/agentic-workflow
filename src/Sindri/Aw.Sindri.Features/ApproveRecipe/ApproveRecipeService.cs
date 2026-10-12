using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryLedger;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// Approves a registered recipe hash (engine spec §4.1, §4.2, W8, W9): plans the approval again, so the pins and preview
/// are the build's now, redeems the challenge against that plan's action and subject, then records 'recipe.approved'
/// and the row. A refused plan or challenge records nothing. Re-approving an approved hash with new pins is a drift
/// approval: its challenge binds the pins digest, and it resumes the instances parked on that drift.
/// </summary>
/// <param name="planner">Plans the approval.</param>
/// <param name="challenges">The server's challenges.</param>
/// <param name="writes">Records the event, the row and the wake.</param>
/// <param name="callers">The caller: the approver and the command id.</param>
internal sealed class ApproveRecipeService(RecipeApprovalPlanner planner, IChallenges challenges, RecipeRegistryWrites writes, ICallerContextAccessor callers)
    : IService<ApproveRecipeRequest, ApproveRecipeResponse>
{
    public async ValueTask<Result<ApproveRecipeResponse>> Handle(ApproveRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("ApproveRecipe needs a caller");
        }

        var plan = await planner.Approve(request.Id, request.Hash, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var redeemed = challenges.Redeem(request.Answer, plan.Value.Action, plan.Value.Subject, WorkflowErrors.RecipeChallengeStale);
        if (redeemed.IsError)
        {
            return redeemed.Error;
        }

        var written = await writes.Approve(plan.Value, caller.Actor, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new ApproveRecipeResponse(new CommandAccepted(caller.CommandId, written.Value.Sequence), written.Value.Approval, plan.Value.Preview,
                plan.Value.Preview.Resumes);
    }
}
