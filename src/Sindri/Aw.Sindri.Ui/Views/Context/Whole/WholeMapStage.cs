using Avalonia;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>
/// Where the whole map's stage and the mini-map over its bottom-left corner sit (q86): the canvas ends above the mini-map, so a fitted map never has a card under it.
/// The view reads these, and so does the test that checks no card is covered.
/// </summary>
internal static class WholeMapStage
{
    /// <summary>The mini-map's width.</summary>
    internal const double MiniMapWidth = 180;

    /// <summary>The mini-map's height.</summary>
    internal const double MiniMapHeight = 112;

    /// <summary>The space round the mini-map.</summary>
    internal const double MiniMapMargin = 14;

    /// <summary>The height under the canvas that the mini-map takes: the canvas stops above it.</summary>
    internal const double ReservedBottom = MiniMapHeight + (2 * MiniMapMargin);

    /// <summary>Gets the mini-map's margin.</summary>
    internal static Thickness MiniMapMarginThickness { get; } = new(MiniMapMargin);

    /// <summary>Gets the canvas's margin: nothing under the mini-map.</summary>
    internal static Thickness CanvasMargin { get; } = new(0, 0, 0, ReservedBottom);

    /// <summary>Where the mini-map sits on a stage of a height.</summary>
    /// <param name="stageHeight">The whole stage's height (the canvas and the strip under it).</param>
    /// <returns>The mini-map's rectangle.</returns>
    internal static Rect MiniMapRect(double stageHeight) => new(MiniMapMargin, stageHeight - MiniMapMargin - MiniMapHeight, MiniMapWidth, MiniMapHeight);
}
