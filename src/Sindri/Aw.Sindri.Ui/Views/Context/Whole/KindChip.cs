using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>One node chip to draw: the node, its area's border colour, the chip's fill and ink, and whether it is the pinned one.</summary>
/// <param name="Node">The node.</param>
/// <param name="Area">The area's colour, for the border.</param>
/// <param name="Fill">The chip's fill.</param>
/// <param name="Ink">The label's colour.</param>
/// <param name="Focused">True for the pinned node.</param>
internal readonly record struct KindChip(PlacedNode Node, IBrush Area, IBrush Fill, IBrush Ink, bool Focused);
