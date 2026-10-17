using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Linking;
using Aw.Kernel;

namespace Aw.Context.Core.Subgraph;

/// <summary>Builds the subgraph view (scoping spec C14). Pure over an applied graph.</summary>
public static class SubgraphBuilder
{
    /// <summary>Builds the view.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see.</param>
    /// <param name="keys">The node keys as given.</param>
    /// <param name="depth">How far to widen, 0 to 4.</param>
    /// <param name="conflicts">The model-checked decision pairs.</param>
    /// <returns>The view; SND-CONTEXT-006 for no key, more than 1000, a malformed key or a depth outside 0 to 4.</returns>
    public static Result<SubgraphView> Build(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> keys, int depth, IReadOnlyList<DecisionConflict> conflicts) =>
        throw new NotSupportedException();
}
