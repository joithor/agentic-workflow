using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// The recipes this build ships and where they stand (setup-once spec §5.3): the one port the workflow engine implements for approve-all. Its plan
/// is computed from the build's embedded set, the registry and the approvals with <see cref="CapabilityId"/>s (the engine's RecipeCapabilitySet); its apply runs the engine's internal
/// ApproveShippedRecipe over a <see cref="RedeemedPlan"/>, with the same checks as ApproveRecipe (pins, evals declared, the capability ceiling,
/// the approved profile's allowlist) and no challenge per recipe. This API holds no engine: it calls this port.
/// </summary>
public interface IShippedRecipes
{
    /// <summary>The plan now: every shipped recipe that is registered and not approved at its current hash, or whose approval is stale, children first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The plan (empty rows when nothing waits, digest <see cref="ApproveAllDigest.Empty"/>).</returns>
    public ValueTask<Result<ShippedRecipePlan>> Plan(CancellationToken cancellationToken);

    /// <summary>
    /// Approves each recipe of <paramref name="plan"/> in order. It first checks that the row's 'id@hash' is the plan's row and is in the embedded set (a mismatch is
    /// SND-RECIPE-009 for that recipe, one not shipped SND-RECIPE-011). A refusal skips that recipe and every recipe that waits on it, and the work carries on with
    /// the rest; one 'recipe.approved' is written for each approval, carrying the plan's digest.
    /// </summary>
    /// <param name="plan">The plan a challenge was redeemed for.</param>
    /// <param name="progress">Takes one progress line per recipe (its display name).</param>
    /// <param name="cancellationToken">Cancels the work between recipes.</param>
    /// <returns>What was approved, refused and skipped; an error only when the work could not run at all.</returns>
    public ValueTask<Result<ApproveAllResult>> Approve(RedeemedPlan plan, IProgress<string> progress, CancellationToken cancellationToken);
}
