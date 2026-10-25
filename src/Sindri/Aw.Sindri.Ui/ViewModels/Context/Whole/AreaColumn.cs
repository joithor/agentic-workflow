namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One area's column on the node-level map.</summary>
/// <param name="Key">The area's key.</param>
/// <param name="Label">The label.</param>
/// <param name="AreaIndex">The area's index in the order.</param>
/// <param name="Rect">The column's rectangle, header included.</param>
internal sealed record AreaColumn(string Key, string Label, int AreaIndex, WholeRect Rect);
