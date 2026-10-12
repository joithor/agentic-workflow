using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// 'recipe eval --scaffold' (spec §6.3, §9.2): a happy-path case for a new recipe, line 1 marked '"scaffold": true'.
/// From each state it tries the source's declared outcomes in order (a Step's, an event's, the outcomes a child wait's
/// transitions name) and keeps the first whose transition the engine applies into a state not yet visited, until a
/// terminal state or a state nothing moves on from. Every gate passes. Each line's 'expect' is what the engine did, so
/// the printed case passes.
/// </summary>
public static class EvalScaffold
{
    /// <summary>The happy-path case of <paramref name="recipe"/>; a child wait's child doesn't resolve.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors (the Steps' declared outcomes and outputs).</param>
    /// <returns>The case, named 'scaffold'.</returns>
    public static EvalCase Generate(Recipe recipe, BlockCatalog catalog) =>
        throw new NotSupportedException("P3 unit (f): EvalScaffold.Generate");

    /// <summary>The happy-path case of <paramref name="recipe"/>, its child waits' children resolved by <paramref name="children"/>.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors.</param>
    /// <param name="children">The recipe's child recipes.</param>
    /// <returns>The case, named 'scaffold'.</returns>
    public static EvalCase Generate(Recipe recipe, BlockCatalog catalog, IChildRecipeResolver children) =>
        throw new NotSupportedException("P3 unit (f): EvalScaffold.Generate");
}
