using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// The recipe approvals (spec §4.1, §7.1, W8, W11): rows keyed by (id, version, hash) with their pins, and the
/// revocations. Approvals live in ledger.db and travel with a host handover.
/// </summary>
public interface IRecipeApprovals
{
    /// <summary>Appends <paramref name="approval"/>: the first approval of its key, or a drift approval of the same hash with new pins.</summary>
    /// <param name="approval">The approval row.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored row, or SND-RECIPE-005 when the hash is revoked (a revoked hash can't be re-approved).</returns>
    public ValueTask<Result<RecipeApproval>> Approve(RecipeApproval approval, CancellationToken cancellationToken);

    /// <summary>The latest approval of (<paramref name="id"/>, <paramref name="version"/>, <paramref name="hash"/>).</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <param name="hash">Its hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The latest row, or null when that key was never approved.</returns>
    public ValueTask<Result<RecipeApproval?>> Latest(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken);

    /// <summary>Every approval row of the key, oldest first (the first approval, then each drift approval).</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <param name="hash">Its hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The rows (empty when never approved).</returns>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> History(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken);

    /// <summary>Records <paramref name="revocation"/>. Revoking a hash already revoked returns the earlier row and records nothing.</summary>
    /// <param name="revocation">The revocation.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The stored row, or SND-RECIPE-003 when the hash was never approved.</returns>
    public ValueTask<Result<RecipeRevocation>> Revoke(RecipeRevocation revocation, CancellationToken cancellationToken);

    /// <summary>The revocation of <paramref name="hash"/>.</summary>
    /// <param name="hash">The hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The row, or null when the hash isn't revoked.</returns>
    public ValueTask<Result<RecipeRevocation?>> Revocation(RecipeHash hash, CancellationToken cancellationToken);

    /// <summary>The latest approvals whose base is <paramref name="baseHash"/> and that aren't revoked: what a revoke of it cascades to.</summary>
    /// <param name="baseHash">The base's hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approvals, by id and version.</returns>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> ApprovedFrom(RecipeHash baseHash, CancellationToken cancellationToken);

    /// <summary>
    /// The latest approval of every key whose base is <paramref name="baseHash"/>, revoked or not: the edges a revoke walks
    /// through, so a revoke that stopped partway reaches what lies below a recipe it already stamped.
    /// </summary>
    /// <param name="baseHash">The base's hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approvals, by id and version.</returns>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> GeneratedFrom(RecipeHash baseHash, CancellationToken cancellationToken);

    /// <summary>The latest approvals that pin <paramref name="childHash"/> as a child and aren't revoked: the parents a child change marks child-stale or wakes.</summary>
    /// <param name="childHash">The child's hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approvals, by id and version.</returns>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Pinning(RecipeHash childHash, CancellationToken cancellationToken);

    /// <summary>The latest approval of every key that isn't revoked, by id and version.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approvals.</returns>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Current(CancellationToken cancellationToken);
}
