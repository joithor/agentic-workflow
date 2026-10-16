using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Linking;

/// <summary>Merges the intent layer into a graph (S-5B): 'record:' nodes, 'mentions' edges, decision nodes with 'quotes', 'decides' and 'supersedes'. Targets resolve through aliases.</summary>
public static class IntentOverlay
{
    /// <summary>Applies the layer.</summary>
    /// <param name="graph">The graph (a map version's, or an applied one).</param>
    /// <param name="layer">What syncs wrote.</param>
    /// <returns>The merged graph and the orphaned links; the same graph when the layer is empty.</returns>
    public static IntentOverlayResult Apply(GraphSnapshot graph, IntentLayer layer) =>
        throw new NotSupportedException("S-5B slice 1: the overlay is not written yet");
}
