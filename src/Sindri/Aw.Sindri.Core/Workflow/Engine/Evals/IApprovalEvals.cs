using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// The hook that makes approving a recipe run its evals (engine spec "Phase 4": "approve running the evals"; §4.3, §9.2;
/// review E3: "P4 wires approve to run them"). Engine P2's 'ApproveRecipe' calls it after the challenge and before it
/// records 'recipe.approved'; a recipe whose evals are missing or failing is refused with SND-RECIPE-008 and nothing is
/// recorded. Engine P4 unit (e) names the port; it is bound when P2's approval slice lands, by moving the 'EvalRecipe'
/// slice's case reading and running (<see cref="EvalRunner"/>) into Core so both slices share it.
/// </summary>
public interface IApprovalEvals
{
    /// <summary>Runs every eval <paramref name="recipe"/> lists against it.</summary>
    /// <param name="recipe">The recipe being approved, with its hash.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>Done when every eval passes, or SND-RECIPE-008 naming the first missing or failing case.</returns>
    public ValueTask<Result<Unit>> Check(Recipe recipe, CancellationToken cancellationToken);
}
