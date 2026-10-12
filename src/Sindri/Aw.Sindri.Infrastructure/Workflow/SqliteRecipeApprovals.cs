using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The recipe approvals in ledger.db's 'recipe_approvals' table (engine spec §4.1). Red stub.</summary>
internal sealed class SqliteRecipeApprovals : IRecipeApprovals
{
    public SqliteRecipeApprovals(LedgerDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
    }

    public ValueTask<Result<RecipeApproval>> Approve(RecipeApproval approval, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<RecipeApproval?>> Latest(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> History(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<RecipeRevocation>> Revoke(RecipeRevocation revocation, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<RecipeRevocation?>> Revocation(RecipeHash hash, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> ApprovedFrom(RecipeHash baseHash, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Pinning(RecipeHash childHash, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Current(CancellationToken cancellationToken) => throw new NotSupportedException();
}
