using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the lenses view around one node (spec §8.9, S-4). Pure over an applied graph.</summary>
public static class LensesBuilder
{
    /// <summary>The cap on each list of a band.</summary>
    public const int BandCap = 100;

    /// <summary>Builds the view.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see: overrides applied, a session's scope already cut.</param>
    /// <param name="anchor">The anchor (a key or an alias).</param>
    /// <param name="depth">How far the neighbors reach and the cascade runs, 0 to 4.</param>
    /// <returns>The view, or SND-CONTEXT-003 when the graph has no such node.</returns>
    public static Result<LensesView> Build(LensBasis basis, GraphSnapshot graph, NodeKey anchor, int depth) =>
        throw new NotSupportedException();
}
