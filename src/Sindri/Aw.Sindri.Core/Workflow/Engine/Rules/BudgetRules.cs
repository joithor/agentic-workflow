using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Budgets and token grants (spec §3.7, §5.5, W7): spend against the state's and the recipe's budget, what is left, and '--grant-tokens n'. P3 unit (c).</summary>
public static class BudgetRules
{
    /// <summary>A run's spend (spec §3.7): exhausting the state's or the recipe's budget parks with budget (kind step, resume rerun).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="tokens">The tokens the run spent.</param>
    /// <returns>The new spend, and the park when a budget ran out.</returns>
    public static BudgetVerdict Spend(WorkflowInstance instance, Recipe recipe, int tokens) =>
        throw new NotSupportedException("P3 unit (c): BudgetRules.Spend");

    /// <summary>The tokens left ('{budget.tokens.left}', and what 'gate.budget.available' reads): the lower of the state's and the recipe's, grants included.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The tokens left; null when no budget applies.</returns>
    public static int? Left(WorkflowInstance instance, Recipe recipe) =>
        throw new NotSupportedException("P3 unit (c): BudgetRules.Left");

    /// <summary>'--grant-tokens n' (W7): on a budget park only, at most the original budget, once per park.</summary>
    /// <param name="instance">The parked instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="tokens">n.</param>
    /// <returns>The instance with the grant added, or SND-WF-003.</returns>
    public static Result<WorkflowInstance> GrantTokens(WorkflowInstance instance, Recipe recipe, int tokens) =>
        throw new NotSupportedException("P3 unit (c): BudgetRules.GrantTokens");
}
