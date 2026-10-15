namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>The path order of a recipe's states (spec §3.10). A pure function of the transition graph.</summary>
public static class StatePathOrder
{
    /// <summary>The path order of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">A bound recipe.</param>
    /// <returns>The order, the branches and the loop edges.</returns>
    public static PathOrder Of(Recipe recipe) => throw new NotSupportedException();
}
