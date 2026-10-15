namespace Aw.Context.Core.Graph;

/// <summary>'context neighbors &lt;key&gt; [--depth N]' (spec §8.5): the nodes within N edges, either way, and the edges among them.</summary>
/// <param name="MapVersion">The map version read.</param>
/// <param name="Center">The center's key (an alias resolved).</param>
/// <param name="Depth">The depth asked (default 1).</param>
/// <param name="Nodes">The nodes by depth, then key; the center first.</param>
/// <param name="Edges">The edges among them.</param>
public sealed record NeighborsView(string MapVersion, NodeKey Center, int Depth, IReadOnlyList<NeighborFact> Nodes, IReadOnlyList<EdgeFact> Edges);
