namespace Aw.Context.Core.Graph;

/// <summary>The cascade (spec §6.1): what a change to an anchor affects, computed per query over typed steps. Pure.</summary>
public static class Cascade
{
    /// <summary>The anchor's own scope and the nodes up to <paramref name="depth"/> steps from it.</summary>
    /// <param name="graph">The graph a caller may see (overrides applied, a session's scope already cut).</param>
    /// <param name="anchor">A node key in <paramref name="graph"/> (resolve aliases first).</param>
    /// <param name="depth">The depth cap, 0 to 4 (0: own scope only).</param>
    /// <returns>The own scope and the affected nodes.</returns>
    public static CascadeResult Affects(GraphSnapshot graph, NodeKey anchor, int depth) => throw new NotSupportedException();
}
