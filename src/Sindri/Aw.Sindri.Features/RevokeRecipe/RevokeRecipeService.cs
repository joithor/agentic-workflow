using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.RevokeRecipe;

/// <summary>
/// Revokes an approved recipe hash (engine spec §4.1, §4.2, W11): plans the revoke again, redeems the challenge against
/// that plan, then records 'recipe.revoked' (through RecipeEvents) and the row for the hash and for each generated
/// recipe approved from it, and wakes their running instances so they park. Each event comes before its row, so a
/// refused or failed append leaves no row behind. A refused plan records nothing.
/// </summary>
/// <param name="planner">Plans the revoke.</param>
/// <param name="events">Appends the registry's events.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="wake">Reaches the running instances.</param>
/// <param name="clock">When the row says it happened.</param>
/// <param name="callers">The caller: who revoked it and the command id.</param>
internal sealed class RevokeRecipeService(
    RecipeRevokePlanner planner,
    RecipeEvents events,
    IRecipeApprovals approvals,
    IRecipeWake wake,
    TimeProvider clock,
    ICallerContextAccessor callers)
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

        var written = await Record(plan.Value, caller.Actor, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new RevokeRecipeResponse(new CommandAccepted(caller.CommandId, written.Value.Sequence), written.Value.Revocation, written.Value.Cascade, plan.Value.Preview);
    }

    /// <summary>The revoked hash, then each generated recipe approved from it; returns the revoked hash's event sequence.</summary>
    private async ValueTask<Result<(RecipeRevocation Revocation, IReadOnlyList<RecipeRevocation> Cascade, long Sequence)>> Record(
        RevokePlan plan, string actor, CancellationToken cancellationToken)
    {
        var recipe = plan.Recipe;
        var root = await RevokeOne(new RecipeRevocation(recipe.Id, recipe.Version, recipe.Hash, actor, clock.GetUtcNow(), CascadedFrom: null), cancellationToken)
            .ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var cascaded = new List<RecipeRevocation>();
        foreach (var approval in plan.Cascade)
        {
            var from = approval.Base?.Hash ?? recipe.Hash;
            var one = await RevokeOne(new RecipeRevocation(approval.Id, approval.Version, approval.Hash, actor, clock.GetUtcNow(), from), cancellationToken)
                .ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            cascaded.Add(one.Value.Revocation);
        }

        return (root.Value.Revocation, cascaded, root.Value.Sequence);
    }

    /// <summary>One revoked hash: its event (keyed by the hash, so a retry records nothing new), its row, and its wake.</summary>
    private async ValueTask<Result<(RecipeRevocation Revocation, long Sequence)>> RevokeOne(RecipeRevocation revocation, CancellationToken cancellationToken)
    {
        var appended = await events.Revoked(revocation, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var stored = await approvals.Revoke(revocation, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var notice = new RecipeWakeNotice(revocation.Hash, RecipeWakeKind.Revoked, appended.Value.Sequence, stored.Value.RevokedBy);   // a resumed revoke replays the first revoker's
        var woken = await wake.Wake(notice, cancellationToken).ConfigureAwait(false);
        return woken.IsError ? woken.Error : (stored.Value, appended.Value.Sequence);
    }
}
