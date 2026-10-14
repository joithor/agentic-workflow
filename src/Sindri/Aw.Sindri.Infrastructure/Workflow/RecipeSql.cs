namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The recipe registry's SQL (ledger.db, migration 0004_recipes). JSON1 lookups stay here, named.</summary>
internal static class RecipeSql
{
    private const string RecipeColumns = "id, version, hash, text, base_id, base_hash, registered_at";

    internal const string SelectRecipeByHash = "SELECT " + RecipeColumns + " FROM recipes WHERE hash = $hash";

    internal const string SelectRecipeByKey = "SELECT " + RecipeColumns + " FROM recipes WHERE id = $id AND version = $version";

    internal const string SelectVersions = "SELECT " + RecipeColumns + " FROM recipes WHERE id = $id ORDER BY version";

    internal const string SelectAllRecipes = "SELECT " + RecipeColumns + " FROM recipes ORDER BY id, version";

    internal const string InsertRecipe =
        "INSERT INTO recipes (hash, id, version, text, base_id, base_hash, registered_at) " +
        "VALUES ($hash, $id, $version, $text, $baseId, $baseHash, $registeredAt)";

    private const string ApprovalColumns = "id, version, hash, approver, approved_at, via, base_id, base_hash, blocks_json, children_json";

    internal const string InsertApproval =
        "INSERT INTO recipe_approvals (id, version, hash, approver, approved_at, via, base_id, base_hash, blocks_json, children_json) " +
        "VALUES ($id, $version, $hash, $approver, $approvedAt, $via, $baseId, $baseHash, $blocks, $children)";

    internal const string SelectLatestApproval =
        "SELECT " + ApprovalColumns + " FROM recipe_approvals WHERE id = $id AND version = $version AND hash = $hash ORDER BY seq DESC LIMIT 1";

    internal const string SelectApprovalHistory =
        "SELECT " + ApprovalColumns + " FROM recipe_approvals WHERE id = $id AND version = $version AND hash = $hash ORDER BY seq";

    internal const string IsApproved = "SELECT EXISTS (SELECT 1 FROM recipe_approvals WHERE hash = $hash)";

    internal const string IsRevoked = "SELECT EXISTS (SELECT 1 FROM recipe_approvals WHERE hash = $hash AND revoked_at IS NOT NULL)";

    internal const string SelectRevocation =
        "SELECT id, version, hash, revoked_by, revoked_at, cascaded_from FROM recipe_approvals " +
        "WHERE hash = $hash AND revoked_at IS NOT NULL ORDER BY seq LIMIT 1";

    internal const string RevokeHash =
        "UPDATE recipe_approvals SET revoked_by = $revokedBy, revoked_at = $revokedAt, cascaded_from = $cascadedFrom " +
        "WHERE hash = $hash AND revoked_at IS NULL";

    // The latest row of each key whose hash isn't revoked (a revoke stamps every row of the hash).
    private const string CurrentRows =
        "SELECT " + ApprovalColumns + " FROM recipe_approvals a WHERE a.revoked_at IS NULL AND a.seq = " +
        "(SELECT MAX(b.seq) FROM recipe_approvals b WHERE b.id = a.id AND b.version = a.version AND b.hash = a.hash)";

    // The latest row of each key, revoked or not.
    private const string LatestRows =
        "SELECT " + ApprovalColumns + " FROM recipe_approvals a WHERE a.seq = " +
        "(SELECT MAX(b.seq) FROM recipe_approvals b WHERE b.id = a.id AND b.version = a.version AND b.hash = a.hash)";

    private const string ByKey = " ORDER BY a.id, a.version, a.seq";

    internal const string SelectCurrent = CurrentRows + ByKey;

    internal const string SelectGeneratedFrom = LatestRows + " AND a.base_hash = $baseHash" + ByKey;

    internal const string SelectPinning =
        CurrentRows + " AND EXISTS (SELECT 1 FROM json_each(a.children_json) c WHERE c.value = $childHash)" + ByKey;
}
