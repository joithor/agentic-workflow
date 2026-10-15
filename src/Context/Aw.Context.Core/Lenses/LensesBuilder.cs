using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Core.Lenses;

/// <summary>Builds the lenses view around one node (spec §8.8, S-4). Pure over an applied graph.</summary>
public static class LensesBuilder
{
    /// <summary>The cap on each band's items.</summary>
    public const int BandCap = 100;

    /// <summary>Builds the view.</summary>
    /// <param name="map">The map read ('v13@7c3e1a2').</param>
    /// <param name="draft">True when the draft was read.</param>
    /// <param name="graph">The graph a caller may see: overrides applied, a session's scope already cut.</param>
    /// <param name="anchor">The anchor (a key or an alias).</param>
    /// <returns>The view, or SND-CONTEXT-003 when the graph has no such node.</returns>
    public static Result<LensesView> Build(string map, bool draft, GraphSnapshot graph, NodeKey anchor)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(graph);
        _ = (draft, anchor);
        throw new NotSupportedException("S-4: LensesBuilder.Build is not built yet");
    }
}
