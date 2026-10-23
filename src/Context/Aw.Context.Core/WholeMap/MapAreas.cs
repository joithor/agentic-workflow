using Aw.Context.Core.Graph;

namespace Aw.Context.Core.WholeMap;

/// <summary>Gives every node of a map one area by fixed rules over the edges it already has (Part D, §2.2). Pure: the same graph always gives the same areas, whatever the order of its rows.</summary>
public static class MapAreas
{
    /// <summary>The areas of <paramref name="graph"/>.</summary>
    /// <param name="graph">One map version's graph.</param>
    /// <returns>Each node's area.</returns>
    public static AreaIndex Assign(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new AreaAssigner(graph).Run();
    }
}
