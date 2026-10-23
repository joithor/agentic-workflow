namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Where everything of the whole map sits (whole-map spec §3.2): pure numbers, the same for the same answer.</summary>
/// <param name="World">The world's rectangle.</param>
/// <param name="ByArea">True for the overview of tiles; false for nodes in columns.</param>
/// <param name="Order">The areas' keys, left to right (and row by row for tiles).</param>
/// <param name="Columns">The area columns; empty by area.</param>
/// <param name="Nodes">The nodes; empty by area.</param>
/// <param name="Tiles">The tiles; empty on a node-level map.</param>
/// <param name="Edges">The node edges; empty by area.</param>
/// <param name="AreaEdges">The edges between areas, between tiles; empty on a node-level map.</param>
/// <param name="Bands">The band extents; empty by area.</param>
internal sealed record WholeMapGeometry(
    WholeRect World,
    bool ByArea,
    IReadOnlyList<string> Order,
    IReadOnlyList<AreaColumn> Columns,
    IReadOnlyList<PlacedNode> Nodes,
    IReadOnlyList<PlacedTile> Tiles,
    IReadOnlyList<PlacedEdge> Edges,
    IReadOnlyList<PlacedAreaEdge> AreaEdges,
    IReadOnlyList<PlacedBand> Bands);
