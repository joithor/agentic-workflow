using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.RegistryLedger;

namespace Aw.Sindri.Features.RevokeRecipe;

/// <summary>
/// Revokes an approved recipe hash (engine spec §4.1, §4.2, W11): plans the revoke again, redeems the challenge against
/// that plan, then records 'recipe.revoked' and the row for the hash and for each generated recipe approved from it,
/// and wakes their running instances so they park. A refused plan or challenge records nothing.
/// </summary>
/// <param name="planner">Plans the revoke.</param>
/// <param name="challenges">The server's challenges.</param>
/// <param name="writes">Records the events, the rows and the wakes.</param>
/// <param name="callers">The caller: who revoked it and the command id.</param>
internal sealed class RevokeRecipeService(RecipeRevokePlanner planner, IChallenges challenges, RecipeRegistryWrites writes, ICallerContextAccessor callers)
    : IService<RevokeRecipeRequest, RevokeRecipeResponse>
{
    public async ValueTask<Result<RevokeRecipeResponse>> Handle(RevokeRecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("RevokeRecipe needs a caller");
        }

        var plan = await planner.Revoke(request.Id, request.Hash.Value, cancellationToken).ConfigureAwait(false);
        if (plan.IsError)
        {
            return plan.Error;
        }

        var redeemed = challenges.Redeem(request.Answer, plan.Value.Action, plan.Value.Subject, WorkflowErrors.RecipeChallengeStale);
        if (redeemed.IsError)
        {
            return redeemed.Error;
        }

        var written = await writes.Revoke(plan.Value, caller.Actor, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new RevokeRecipeResponse(new CommandAccepted(caller.CommandId, written.Value.Sequence), written.Value.Revocation, written.Value.Cascade, plan.Value.Preview);
    }
}
