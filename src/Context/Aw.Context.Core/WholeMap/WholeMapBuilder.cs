using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.WholeMap;

/// <summary>Builds the whole-map answer from a graph and its areas (Part D, §2.1). Pure apart from the text policy it writes labels with.</summary>
public static class WholeMapBuilder
{
    /// <summary>The answer for one graph.</summary>
    /// <param name="ask">The map version, the detail and the keys to place.</param>
    /// <param name="graph">The graph the caller may see.</param>
    /// <param name="areas">Its areas.</param>
    /// <param name="rules">The limits.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view.</returns>
    public static WholeMapView Build(WholeMapAsk ask, GraphSnapshot graph, AreaIndex areas, WholeMapRules rules, MapText text) =>
        throw new NotSupportedException();
}
