namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The edges of one type between two areas, as a line between their tiles' centres.</summary>
/// <param name="From">The source area.</param>
/// <param name="To">The target area.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Count">How many edges.</param>
/// <param name="Confirmed">How many are confirmed.</param>
/// <param name="X1">The line's start x.</param>
/// <param name="Y1">The line's start y.</param>
/// <param name="X2">The line's end x.</param>
/// <param name="Y2">The line's end y.</param>
internal sealed record PlacedAreaEdge(string From, string To, string Type, int Count, int Confirmed, double X1, double Y1, double X2, double Y2);
