
namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>Everything the map draws: the world's size, the three bands, the nodes and the edges, all in world coordinates.</summary>
/// <param name="World">The world's rectangle (1360 by 872, taller when a row wrapped).</param>
/// <param name="Bands">The three band frames.</param>
/// <param name="Nodes">The nodes, in reading order: intent, product, code.</param>
/// <param name="Edges">The edges, de-duplicated.</param>
internal sealed record MapLayout(MapRect World, IReadOnlyList<MapBandLayout> Bands, IReadOnlyList<MapNodeLayout> Nodes, IReadOnlyList<MapEdgeLayout> Edges);
