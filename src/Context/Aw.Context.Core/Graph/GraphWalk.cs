namespace Aw.Context.Core.Graph;

/// <summary>Neighbors and paths over the graph (spec §8.5 'neighbors', 'path'), edges walked in either direction. Pure.</summary>
public static class GraphWalk
{
    /// <summary>The deepest neighborhood a request may ask for (spec §8.3).</summary>
    public const int MaxDepth = 4;

    /// <summary>The longest path searched (spec §8.3).</summary>
    public const int MaxPathLength = 8;

    /// <summary>The nodes within <paramref name="depth"/> edges of <paramref name="center"/>.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="center">A node key in <paramref name="graph"/>.</param>
    /// <param name="depth">0 to <see cref="MaxDepth"/>.</param>
    /// <returns>The neighborhood.</returns>
    public static Neighborhood Neighbors(GraphSnapshot graph, NodeKey center, int depth) => throw new NotSupportedException();

    /// <summary>The shortest path from <paramref name="from"/> to <paramref name="to"/>, at most <see cref="MaxPathLength"/> edges.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="from">A node key in <paramref name="graph"/>.</param>
    /// <param name="to">A node key in <paramref name="graph"/>.</param>
    /// <returns>The path, or null when none is that short.</returns>
    public static GraphPath? ShortestPath(GraphSnapshot graph, NodeKey from, NodeKey to) => throw new NotSupportedException();
}
