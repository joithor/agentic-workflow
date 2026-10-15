using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The approved recipes, read-only, as the interpreter and the start and command slices need them (port; engine spec rev
/// 8 §4, §5.1 'LoadRecipe', §5.2 the approval check). Engine P4's read port over P2's registry (IRecipeStore,
/// IRecipeApprovals): RegistryApprovedRecipes implements it.
/// </summary>
public interface IApprovedRecipes
{
    /// <summary>The latest approved version of <paramref name="id"/> ('workflow start &lt;recipe&gt;').</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recipe, or SND-RECIPE-002 (unknown) or SND-RECIPE-003 (none approved).</returns>
    public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken);

    /// <summary>The approved recipe '<paramref name="id"/>@<paramref name="hash"/>' ('start id@hash', and 'LoadRecipe').</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">Its hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The recipe, or SND-RECIPE-002 or -003.</returns>
    public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken);

    /// <summary>The approval check of a running instance's recipe (RCP-CAP-003, §5.2), before a human trigger's Update is sent.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether it holds, was revoked or drifted.</returns>
    public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken);
}
