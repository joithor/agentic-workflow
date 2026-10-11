using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>'recipe eval --scaffold' (spec §6.3, §9.2): a happy-path case for a new recipe (first matching transitions, first declared outcome), line 1 marked '"scaffold": true'. P3 unit (f).</summary>
public static class EvalScaffold
{
    /// <summary>The happy-path case of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="catalog">The build's descriptors (the Steps' declared outcomes and outputs).</param>
    /// <returns>The case, named 'scaffold'.</returns>
    public static EvalCase Generate(Recipe recipe, BlockCatalog catalog) =>
        throw new NotSupportedException("P3 unit (f): EvalScaffold.Generate");
}
