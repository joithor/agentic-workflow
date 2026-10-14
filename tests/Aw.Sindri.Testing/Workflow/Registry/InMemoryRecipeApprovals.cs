using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Testing.Workflow.Registry;

/// <summary>The recipe approvals and revocations in memory (<see cref="RecipeApprovalsContract"/> keeps it honest).</summary>
public sealed class InMemoryRecipeApprovals : IRecipeApprovals
{
    private readonly List<RecipeApproval> _approvals = [];
    private readonly List<RecipeRevocation> _revocations = [];
    private readonly Lock _gate = new();

    /// <inheritdoc/>
    public ValueTask<Result<RecipeApproval>> Approve(RecipeApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        lock (_gate)
        {
            if (_revocations.Exists(r => r.Hash == approval.Hash))
            {
                return Done<RecipeApproval>(WorkflowErrors.RecipeRevoked.With($"{approval.Id.Value}@{approval.Hash.Display} was revoked; approve a new version"));
            }

            _approvals.Add(approval);
            return Done<RecipeApproval>(approval);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<RecipeApproval?>> Latest(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done<RecipeApproval?>(_approvals.FindLast(a => a.Id == id && a.Version == version && a.Hash == hash));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> History(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done(Results.Success<IReadOnlyList<RecipeApproval>>([.. _approvals.Where(a => a.Id == id && a.Version == version && a.Hash == hash)]));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<RecipeRevocation>> Revoke(RecipeRevocation revocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        lock (_gate)
        {
            if (_revocations.Find(r => r.Hash == revocation.Hash) is { } earlier)
            {
                return Done<RecipeRevocation>(earlier);
            }

            if (!_approvals.Exists(a => a.Hash == revocation.Hash))
            {
                return Done<RecipeRevocation>(WorkflowErrors.RecipeNotApproved.With($"{revocation.Id.Value}@{revocation.Hash.Display} was never approved, so there is nothing to revoke"));
            }

            _revocations.Add(revocation);
            return Done<RecipeRevocation>(revocation);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<RecipeRevocation?>> Revocation(RecipeHash hash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done<RecipeRevocation?>(_revocations.Find(r => r.Hash == hash));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> ApprovedFrom(RecipeHash baseHash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done(Results.Success<IReadOnlyList<RecipeApproval>>([.. CurrentRows().Where(a => a.Base?.Hash == baseHash)]));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> GeneratedFrom(RecipeHash baseHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red");

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Pinning(RecipeHash childHash, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done(Results.Success<IReadOnlyList<RecipeApproval>>([.. CurrentRows().Where(a => a.Pins.Children.Any(c => c.Hash == childHash))]));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Current(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Done(Results.Success<IReadOnlyList<RecipeApproval>>([.. CurrentRows()]));
        }
    }

    /// <summary>The latest row per key whose hash isn't revoked, by id and version. Call under the lock.</summary>
    private IEnumerable<RecipeApproval> CurrentRows() =>
        _approvals.GroupBy(a => (a.Id, a.Version, a.Hash)).Select(g => g.Last())
            .Where(a => !_revocations.Exists(r => r.Hash == a.Hash))
            .OrderBy(a => a.Id.Value, StringComparer.Ordinal).ThenBy(a => a.Version);

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);
}
