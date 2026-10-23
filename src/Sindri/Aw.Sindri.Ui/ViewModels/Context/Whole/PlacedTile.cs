namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One area's tile on the by-area overview.</summary>
/// <param name="Key">The area's key.</param>
/// <param name="Label">The label.</param>
/// <param name="AreaIndex">The area's index in the order.</param>
/// <param name="Rect">Where the tile sits.</param>
/// <param name="Members">How many nodes it holds.</param>
/// <param name="KindCounts">Its kinds and counts, biggest first.</param>
/// <param name="Unassigned">True for the area of what no feature claims.</param>
internal sealed record PlacedTile(string Key, string Label, int AreaIndex, WholeRect Rect, int Members, IReadOnlyList<KeyValuePair<string, int>> KindCounts, bool Unassigned);
