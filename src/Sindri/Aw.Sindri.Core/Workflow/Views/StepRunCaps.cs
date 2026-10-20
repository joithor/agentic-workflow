using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// The loop cap of each looped state of a recipe, for the Step runs' max attempts. A state is in a loop by the recipe's
/// own field ('loop'), so a state at neither end of the back edge counts too. The cap is the allowance the instance holds
/// for the loop (a granted round raises it) when a cell of the loop is the active state and shows it, else the
/// recipe's cap.
/// </summary>
public static class StepRunCaps
{
    /// <summary>The cap of each state of <paramref name="recipe"/> that is in a loop; a state in no loop has none.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <param name="states">The instance's state cells, whose active round gives the allowance of its loop.</param>
    /// <returns>State id to cap.</returns>
    public static IReadOnlyDictionary<string, int> Of(Recipe recipe, IReadOnlyList<StateCell> states) => throw new NotSupportedException();
}
