using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.RegistryLedger;

/// <summary>
/// Records a planned approval or revoke (engine spec §4.1, W11): the 'recipe.approved' or 'recipe.revoked' event through
/// the ledger chain first, then the approvals row, then the wake. The event comes first so a refused or failed append
/// leaves no row behind. Kept apart from the slices so engine P2 unit (d)'s event-append code can replace the appends.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="held">The lease this process holds (an append needs its grant).</param>
/// <param name="events">Builds the entries, with the caller's actor and correlation.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="wake">Reaches the running instances.</param>
/// <param name="clock">When the row says it happened.</param>
public sealed class RecipeRegistryWrites(ILedgerStore ledger, HeldLease held, EventFactory events, IRecipeApprovals approvals, IRecipeWake wake, TimeProvider clock)
{
    /// <summary>Records <paramref name="plan"/>'s approval by <paramref name="actor"/> at the desk; a re-approval also wakes the instances parked on drift.</summary>
    /// <param name="plan">The plan the challenge was redeemed against.</param>
    /// <param name="actor">The approver, for example 'human:cli'.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored row and the event's sequence.</returns>
    public async ValueTask<Result<ApprovalWritten>> Approve(ApprovalPlan plan, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var recipe = plan.Recipe;
        var after = await ledger.Latest(EventTypes.RecipeApproved, cancellationToken).ConfigureAwait(false);
        if (after.IsError)
        {
            return after.Error;
        }

        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.RecipeApproved}:{recipe.Hash.Value}:after:{after.Value?.Sequence ?? 0}");
        var payload = new RecipeApproved(recipe.Id, recipe.Version, recipe.Hash, ApprovalVia.Desk, recipe.Base, plan.Preview.Pins, plan.Reapproval);
        var appended = await ledger.Append(events.Entry(EventTypes.RecipeApproved, payload, key), grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var row = new RecipeApproval(recipe.Id, recipe.Version, recipe.Hash, actor, clock.GetUtcNow(), ApprovalVia.Desk, recipe.Base, plan.Preview.Pins);
        var stored = await approvals.Approve(row, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var sequence = appended.Value.Sequence;
        var woken = plan.Reapproval
            ? await wake.Wake(new RecipeWakeNotice(recipe.Hash, RecipeWakeKind.Approved, sequence, actor), cancellationToken).ConfigureAwait(false)
            : Unit.Value;
        return woken.IsError ? woken.Error : new ApprovalWritten(stored.Value, sequence);
    }

    /// <summary>Records <paramref name="plan"/>'s revoke by <paramref name="actor"/>, then each cascaded one, and wakes every revoked hash's instances.</summary>
    /// <param name="plan">The plan the challenge was redeemed against.</param>
    /// <param name="actor">Who revoked it.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The revocation, the cascaded ones, and the revoked hash's event sequence.</returns>
    public async ValueTask<Result<RevokeWritten>> Revoke(RevokePlan plan, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var root = await RevokeOne(new RecipeRevocation(plan.Recipe.Id, plan.Recipe.Version, plan.Recipe.Hash, actor, clock.GetUtcNow(), CascadedFrom: null),
            grant, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var cascaded = new List<RecipeRevocation>();
        foreach (var approval in plan.Cascade)
        {
            var from = approval.Base?.Hash ?? plan.Recipe.Hash;
            var one = await RevokeOne(new RecipeRevocation(approval.Id, approval.Version, approval.Hash, actor, clock.GetUtcNow(), from), grant, cancellationToken)
                .ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            cascaded.Add(one.Value.Revocation);
        }

        return new RevokeWritten(root.Value.Revocation, cascaded, root.Value.Sequence);
    }

    /// <summary>One revoked hash: its event (keyed by the hash, so a retry records nothing new), its row, and its wake.</summary>
    private async ValueTask<Result<(RecipeRevocation Revocation, long Sequence)>> RevokeOne(RecipeRevocation revocation, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var payload = new RecipeRevoked(revocation.Id, revocation.Version, revocation.Hash, revocation.CascadedFrom);
        var entry = events.Entry(EventTypes.RecipeRevoked, payload, $"{EventTypes.RecipeRevoked}:{revocation.Hash.Value}");
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var stored = await approvals.Revoke(revocation, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var notice = new RecipeWakeNotice(revocation.Hash, RecipeWakeKind.Revoked, appended.Value.Sequence, revocation.RevokedBy);
        var woken = await wake.Wake(notice, cancellationToken).ConfigureAwait(false);
        return woken.IsError ? woken.Error : (stored.Value, appended.Value.Sequence);
    }
}
