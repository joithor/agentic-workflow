using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The recipe approvals in ledger.db's 'recipe_approvals' table (engine spec §4.1, W8, W11): append-only rows keyed by
/// (id, version, hash) with their pins; the latest row of a key counts. A revoke stamps every row of the hash, so the
/// revocation travels with the approvals in a host handover.
/// </summary>
internal sealed class SqliteRecipeApprovals(LedgerDatabase database) : IRecipeApprovals
{
    public ValueTask<Result<RecipeApproval>> Approve(RecipeApproval approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        return RecipeSessions.Write(database, async ValueTask<Result<RecipeApproval>> (session, token) =>
        {
            var revoked = await RecipeSessions.Exists(session, ByHash(RecipeSql.IsRevoked, approval.Hash), token).ConfigureAwait(false);
            if (revoked.IsError || revoked.Value)
            {
                return revoked.IsError ? revoked.Error : WorkflowErrors.RecipeRevoked.With($"{approval.Id.Value}@{approval.Hash.Display} was revoked; approve a new version");
            }

            var inserted = await RecipeSessions.Execute(session, Insert(approval), token).ConfigureAwait(false);
            return inserted.IsError ? inserted.Error : approval;
        }, cancellationToken);
    }

    public ValueTask<Result<RecipeApproval?>> Latest(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, empty: null,
            (session, token) => RecipeSessions.First(session, ByKey(RecipeSql.SelectLatestApproval, id, version, hash), RecipeApprovalMapper.Instance, token),
            cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> History(RecipeId id, int version, RecipeHash hash, CancellationToken cancellationToken) =>
        List(ByKey(RecipeSql.SelectApprovalHistory, id, version, hash), cancellationToken);

    public ValueTask<Result<RecipeRevocation>> Revoke(RecipeRevocation revocation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        return RecipeSessions.Write(database, async ValueTask<Result<RecipeRevocation>> (session, token) =>
        {
            var earlier = await RecipeSessions.First(session, ByHash(RecipeSql.SelectRevocation, revocation.Hash), RecipeRevocationMapper.Instance, token).ConfigureAwait(false);
            if (earlier.IsError)
            {
                return earlier.Error;
            }

            if (earlier.Value is { } first)
            {
                return first;
            }

            var approved = await RecipeSessions.Exists(session, ByHash(RecipeSql.IsApproved, revocation.Hash), token).ConfigureAwait(false);
            if (approved.IsError || !approved.Value)
            {
                return approved.IsError ? approved.Error : WorkflowErrors.RecipeNotApproved.With(
                    $"{revocation.Id.Value}@{revocation.Hash.Display} was never approved, so there is nothing to revoke");
            }

            var stamped = await RecipeSessions.Execute(session, Stamp(revocation), token).ConfigureAwait(false);
            if (stamped.IsError)
            {
                return stamped.Error;
            }

            var stored = await RecipeSessions.First(session, ByHash(RecipeSql.SelectRevocation, revocation.Hash), RecipeRevocationMapper.Instance, token).ConfigureAwait(false);
            return stored.IsError ? stored.Error : stored.Value ?? throw new InvalidOperationException("The revoke stamped no row of an approved hash.");
        }, cancellationToken);
    }

    public ValueTask<Result<RecipeRevocation?>> Revocation(RecipeHash hash, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, empty: null,
            (session, token) => RecipeSessions.First(session, ByHash(RecipeSql.SelectRevocation, hash), RecipeRevocationMapper.Instance, token),
            cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> ApprovedFrom(RecipeHash baseHash, CancellationToken cancellationToken) =>
        List(new SqlStatement(RecipeSql.SelectApprovedFrom, c => c.Bind("$baseHash", baseHash.Value)), cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> GeneratedFrom(RecipeHash baseHash, CancellationToken cancellationToken) =>
        List(new SqlStatement(RecipeSql.SelectGeneratedFrom, c => c.Bind("$baseHash", baseHash.Value)), cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Pinning(RecipeHash childHash, CancellationToken cancellationToken) =>
        List(new SqlStatement(RecipeSql.SelectPinning, c => c.Bind("$childHash", childHash.Value)), cancellationToken);

    public ValueTask<Result<IReadOnlyList<RecipeApproval>>> Current(CancellationToken cancellationToken) =>
        List(new SqlStatement(RecipeSql.SelectCurrent), cancellationToken);

    private ValueTask<Result<IReadOnlyList<RecipeApproval>>> List(SqlStatement statement, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, [],
            (session, token) => RecipeSessions.List(session, statement, RecipeApprovalMapper.Instance, token), cancellationToken);

    private static SqlStatement ByHash(string sql, RecipeHash hash) => new(sql, c => c.Bind("$hash", hash.Value));

    private static SqlStatement ByKey(string sql, RecipeId id, int version, RecipeHash hash) =>
        new(sql, c => c.Bind("$id", id.Value).Bind("$version", version).Bind("$hash", hash.Value));

    private static SqlStatement Insert(RecipeApproval approval) => new(RecipeSql.InsertApproval, c => c
        .Bind("$id", approval.Id.Value)
        .Bind("$version", approval.Version)
        .Bind("$hash", approval.Hash.Value)
        .Bind("$approver", approval.Approver)
        .Bind("$approvedAt", approval.ApprovedAt)
        .Bind("$via", RecipeRows.Via(approval.Via))
        .Bind("$baseId", approval.Base?.Id.Value)
        .Bind("$baseHash", approval.Base?.Hash.Value)
        .Bind("$blocks", RecipeRows.Blocks(approval.Pins.Blocks))
        .Bind("$children", RecipeRows.Children(approval.Pins.Children)), $"approval of {approval.Id.Value}@{approval.Hash.Display}");

    private static SqlStatement Stamp(RecipeRevocation revocation) => new(RecipeSql.RevokeHash, c => c
        .Bind("$hash", revocation.Hash.Value)
        .Bind("$revokedBy", revocation.RevokedBy)
        .Bind("$revokedAt", revocation.RevokedAt)
        .Bind("$cascadedFrom", revocation.CascadedFrom?.Value), $"revoke of {revocation.Id.Value}@{revocation.Hash.Display}");
}
