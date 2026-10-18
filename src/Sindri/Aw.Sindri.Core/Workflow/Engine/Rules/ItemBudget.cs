using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The item's token budget as a gate reads it (engine P7 plan D7).</summary>
public static class ItemBudget
{
    /// <summary>The tokens the instance may still spend: the lower of its state's and its recipe's budget, grants included ('gate.budget.available' reads it).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The tokens left, never below 0; null when no budget applies.</returns>
    public static int? TokensLeft(WorkflowInstance instance, Recipe recipe) => BudgetRules.Left(instance, recipe);
}
