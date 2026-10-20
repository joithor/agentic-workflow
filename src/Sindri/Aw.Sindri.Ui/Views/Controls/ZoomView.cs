using System.Runtime.InteropServices;
using Avalonia;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The pan and zoom of the context map's world (F3 spec 1 §2): a scale and the screen offset of the world's origin, with
/// the math the <see cref="ZoomBorder"/> applies. Pure, so the clamp, the zoom around the cursor, the fit and the pan into
/// view are tested without a window.
/// </summary>
/// <param name="Scale">World to screen scale, between <see cref="Min"/> and <see cref="Max"/>.</param>
/// <param name="X">Screen x of the world's origin.</param>
/// <param name="Y">Screen y of the world's origin.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ZoomView(double Scale, double X, double Y)
{
    /// <summary>The smallest scale: the 62 % floor, so a node's words stay readable at 900 px.</summary>
    internal const double Min = 0.62;

    /// <summary>The largest scale: the map is never drawn bigger than it was laid out.</summary>
    internal const double Max = 1.0;

    // Less than this much of the world left on the stage and a pan stops: the map can be dragged aside, never lost.
    private const double KeepVisible = 120;

    /// <summary>Gets the view at the layout's own size, in the stage's corner.</summary>
    internal static ZoomView Identity => new(1, 0, 0);

    /// <summary>The scale <paramref name="scale"/> held inside the zoom range.</summary>
    /// <param name="scale">A wanted scale.</param>
    /// <returns>The scale, no smaller than <see cref="Min"/> and no larger than <see cref="Max"/>.</returns>
    internal static double Clamp(double scale) => Math.Clamp(scale, Min, Max);

    /// <summary>The fit of a world on a stage: the scale that shows all of it (clamped), centred where it is smaller than the stage.</summary>
    /// <param name="stage">The stage's size.</param>
    /// <param name="world">The world's size.</param>
    /// <returns>The view. Where the clamp keeps the world wider than the stage, it starts at the left edge and the right side pans.</returns>
    internal static ZoomView Fit(Size stage, Size world)
    {
        if (world.Width <= 0 || world.Height <= 0 || stage.Width <= 0 || stage.Height <= 0)
        {
            return Identity;
        }

        var scale = Clamp(Math.Min(stage.Width / world.Width, stage.Height / world.Height));
        return new ZoomView(scale, Math.Max(0, (stage.Width - (world.Width * scale)) / 2), Math.Max(0, (stage.Height - (world.Height * scale)) / 2));
    }

    /// <summary>Linear blend of two views, for the zoom transition.</summary>
    /// <param name="from">The view at <paramref name="t"/> 0.</param>
    /// <param name="to">The view at <paramref name="t"/> 1.</param>
    /// <param name="t">Progress, 0 to 1.</param>
    /// <returns>The view between them.</returns>
    internal static ZoomView Lerp(ZoomView from, ZoomView to, double t) =>
        new(from.Scale + ((to.Scale - from.Scale) * t), from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));

    /// <summary>The world point under a screen point.</summary>
    /// <param name="screen">A point in the stage.</param>
    /// <returns>The same point in world coordinates.</returns>
    internal Point ToWorld(Point screen) => new((screen.X - X) / Scale, (screen.Y - Y) / Scale);

    /// <summary>The view after zooming by <paramref name="factor"/> with <paramref name="cursor"/> staying on the same world point.</summary>
    /// <param name="cursor">The stage point that stays put.</param>
    /// <param name="factor">The wanted scale factor (above 1 zooms in); the result is clamped.</param>
    /// <returns>The new view.</returns>
    internal ZoomView ZoomAt(Point cursor, double factor)
    {
        var scale = Clamp(Scale * factor);
        var world = ToWorld(cursor);
        return new ZoomView(scale, cursor.X - (world.X * scale), cursor.Y - (world.Y * scale));
    }

    /// <summary>The view after moving the map by a screen distance.</summary>
    /// <param name="dx">Screen pixels to the right.</param>
    /// <param name="dy">Screen pixels down.</param>
    /// <returns>The new view.</returns>
    internal ZoomView PanBy(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

    /// <summary>The view with the world kept at least partly on the stage.</summary>
    /// <param name="stage">The stage's size.</param>
    /// <param name="world">The world's size.</param>
    /// <returns>The nearest view that leaves 120 px of the world visible on each axis.</returns>
    internal ZoomView Kept(Size stage, Size world) => this with
    {
        X = Math.Clamp(X, KeepVisible - (world.Width * Scale), Math.Max(KeepVisible - (world.Width * Scale), stage.Width - KeepVisible)),
        Y = Math.Clamp(Y, KeepVisible - (world.Height * Scale), Math.Max(KeepVisible - (world.Height * Scale), stage.Height - KeepVisible)),
    };

    /// <summary>The smallest pan that brings a world rectangle fully into the stage, clear of what covers its right side.</summary>
    /// <param name="target">The rectangle, in world coordinates.</param>
    /// <param name="stage">The stage's size.</param>
    /// <param name="rightInset">Width of the panel over the stage's right edge (the inspector), 0 when none.</param>
    /// <param name="margin">Space to leave around the rectangle.</param>
    /// <returns>The new view; a rectangle too big for the room lines its top-left corner up with the margin.</returns>
    internal ZoomView Reveal(Rect target, Size stage, double rightInset = 0, double margin = 24)
    {
        var box = new Rect((target.X * Scale) + X, (target.Y * Scale) + Y, target.Width * Scale, target.Height * Scale);
        return PanBy(Shift(box.Left, box.Right, margin, stage.Width - rightInset - margin), Shift(box.Top, box.Bottom, margin, stage.Height - margin));
    }

    // The move along one axis that puts [start, end] inside [low, high]; too big to fit, the start goes to low.
    private static double Shift(double start, double end, double low, double high)
    {
        if (end - start > high - low || start < low)
        {
            return low - start;
        }

        return end > high ? high - end : 0;
    }
}
