namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One area's column on the by-area overview (a tall tile, with a cell per band).</summary>
/// <param name="Key">The area's key.</param>
/// <param name="Label">The label.</param>
/// <param name="AreaIndex">The area's index in the order.</param>
/// <param name="Rect">Where the tile sits.</param>
/// <param name="Members">How many nodes it holds.</param>
/// <param name="KindCounts">Its kinds and counts, biggest first.</param>
/// <param name="Unassigned">True for the area of what no feature claims.</param>
/// <param name="Cells">Its kinds counted in the band each belongs to, one cell per band that has any.</param>
internal sealed record PlacedTile(string Key, string Label, int AreaIndex, WholeRect Rect, int Members, IReadOnlyList<KeyValuePair<string, int>> KindCounts, bool Unassigned, IReadOnlyList<PlacedTileCell>? Cells = null)
{
    /// <summary>Gets the band cells; none when the tile was built without bands.</summary>
    internal IReadOnlyList<PlacedTileCell> BandCells => Cells ?? [];
}
