using Aw.Kernel;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// Appends the registry's events to the ledger chain (engine spec §4.1), under the process's lease grant:
/// 'recipe.registered:&lt;hash&gt;', 'recipe.approved:&lt;hash&gt;:after:&lt;seq&gt;' (so a drift approval of the same hash is its
/// own event) and 'recipe.revoked:&lt;hash&gt;' (one per revoked hash, cascaded ones included). Register, approve and
/// revoke all write through it.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">The event factory (the caller's actor and correlation).</param>
/// <param name="held">The grant this process holds.</param>
public sealed class RecipeEvents(ILedgerStore ledger, EventFactory events, HeldLease held)
{
    /// <summary>Appends 'recipe.registered' for <paramref name="recipe"/>; registering the same hash again records nothing.</summary>
    /// <param name="recipe">The stored recipe.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The event's position (AlreadyRecorded on a repeat), or SND-LEDGER-002 when no lease is held.</returns>
    public ValueTask<Result<LedgerAppend>> Registered(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        _ = (ledger, events, held, recipe, cancellationToken);
        throw new NotSupportedException(Pending);
    }

    /// <summary>
    /// Appends 'recipe.approved' for <paramref name="approval"/>, keyed by the latest approval event it follows. A retry
    /// whose approval is already the latest event records nothing and returns that event.
    /// </summary>
    /// <param name="approval">The stored approval row.</param>
    /// <param name="drift">True for a drift approval (the same hash re-approved with new pins).</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The event's position, or SND-LEDGER-002 when no lease is held.</returns>
    public ValueTask<Result<LedgerAppend>> Approved(RecipeApproval approval, bool drift, CancellationToken cancellationToken)
    {
        _ = (ledger, approval, drift, cancellationToken);
        throw new NotSupportedException(Pending);
    }

    /// <summary>Appends 'recipe.revoked' for <paramref name="revocation"/>, with the base it cascaded from; revoking the same hash again records nothing.</summary>
    /// <param name="revocation">The stored revocation row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The event's position (AlreadyRecorded on a repeat), or SND-LEDGER-002 when no lease is held.</returns>
    public ValueTask<Result<LedgerAppend>> Revoked(RecipeRevocation revocation, CancellationToken cancellationToken)
    {
        _ = (ledger, revocation, cancellationToken);
        throw new NotSupportedException(Pending);
    }

    private const string Pending = "RecipeEvents arrives with engine P2 unit (d)";
}
