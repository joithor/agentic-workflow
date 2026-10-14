using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Map;

/// <summary>How much a map holds.</summary>
/// <param name="Nodes">Nodes.</param>
/// <param name="Edges">Edges.</param>
/// <param name="Aliases">Aliases.</param>
/// <param name="Decisions">Decisions.</param>
/// <param name="Overrides">Overrides.</param>
public sealed record MapCounts(int Nodes, int Edges, int Aliases, int Decisions, int Overrides)
{
    /// <summary>The counts of a graph and its override count.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="overrides">How many overrides.</param>
    /// <returns>The counts.</returns>
    public static MapCounts Of(GraphSnapshot graph, int overrides)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new(graph.Nodes.Count, graph.Edges.Count, graph.Aliases.Count, graph.Decisions.Count, overrides);
    }
}
