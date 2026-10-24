namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One area's kinds in one band on the overview: "3 decisions", "1 record", a line each.</summary>
/// <param name="Band">The band.</param>
/// <param name="Rect">Where the cell sits, inside its tile and its band.</param>
/// <param name="Kinds">The kinds of this band and their counts, biggest first.</param>
internal sealed record PlacedTileCell(WholeBand Band, WholeRect Rect, IReadOnlyList<KeyValuePair<string, int>> Kinds);
