using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// Renews an approval whose pinned blocks moved only by version (owner q93, 2026-10-23; ENG-064, RC-04): the recipe, its hash and its
/// children stay as approved, the block pins take this build's, and a 'recipe.approved' event with the drift flag, under the actor
/// 'service:pin-drift', is the log of it. A running instance then finds its approval holding instead of parking. A pin whose interface
/// changed is not renewed (<see cref="PinRules.Compatible"/>); it still parks until a person or an agent approves it again.
/// </summary>
/// <param name="approvals">The approvals.</param>
/// <param name="events">Appends the registry's events.</param>
/// <param name="wake">Reaches the instances already parked on the old pins.</param>
/// <param name="callers">Where the renewal's actor is entered.</param>
/// <param name="ids">The ids of that caller.</param>
/// <param name="clock">When the renewal happened.</param>
internal sealed class PinRenewals(IRecipeApprovals approvals, RecipeEvents events, IRecipeWake wake, ICallerContextAccessor callers, IIdGenerator ids, TimeProvider clock) : IPinRenewals
{
    private const string Actor = "pin-drift";

    public async ValueTask<Result<bool>> Renew(RecipeApproval standing, IReadOnlyList<BlockPin> build, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(standing);
        ArgumentNullException.ThrowIfNull(build);
        var row = standing with { Approver = CallerContext.Service(Actor, ids).Actor, ApprovedAt = clock.GetUtcNow(), Pins = standing.Pins with { Blocks = build } };
        using var entered = callers.Enter(CallerContext.Service(Actor, ids));
        var appended = await events.Approved(row, drift: true, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        var stored = await approvals.Approve(row, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        // Instances parked on the old pins before this build resume; a failed wake leaves them for the next approval, it doesn't undo the renewal.
        var running = await wake.Running(row.Hash, cancellationToken).ConfigureAwait(false);
        if (running.IsSuccess && running.Value.Count > 0)
        {
            _ = await wake.Wake(new RecipeWakeNotice(row.Hash, RecipeWakeKind.Approved, appended.Value.Sequence, row.Approver), cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
