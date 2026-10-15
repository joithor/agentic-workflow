namespace Aw.Context.Core.Graph;

/// <summary>A node the cascade reached (spec §6.1): its depth and the shortest path that reached it.</summary>
/// <param name="Node">The affected node: the owning feature of a reached surface or action, or the reached node itself when it has none.</param>
/// <param name="Depth">The number of steps (state, dependency and flow hops) from the anchor's own scope.</param>
/// <param name="Path">The path as §8.4 writes it: the anchor's key, then one entry per edge walked ('writes thing:pane-layout', 'reads action:…').</param>
/// <param name="Edges">The edges walked, in order (their provenance decides the item's).</param>
public sealed record AffectedNode(NodeKey Node, int Depth, IReadOnlyList<string> Path, IReadOnlyList<GraphEdge> Edges);
