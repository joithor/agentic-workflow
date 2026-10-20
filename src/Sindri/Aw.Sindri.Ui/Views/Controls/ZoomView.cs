using System.Runtime.InteropServices;
using Avalonia;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The pan and zoom of the context map's world (F3 spec 1 §2); the math is pure so it is tested without a window.</summary>
/// <param name="Scale">World to screen scale, between <see cref="Min"/> and <see cref="Max"/>.</param>
/// <param name="X">Screen x of the world's origin.</param>
/// <param name="Y">Screen y of the world's origin.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct ZoomView(double Scale, double X, double Y)
{
    /// <summary>The smallest scale: the 62 % floor.</summary>
    internal const double Min = 0.62;

    /// <summary>The largest scale.</summary>
    internal const double Max = 1.0;

    /// <summary>Gets the view at the layout's own size, in the stage's corner.</summary>
    internal static ZoomView Identity => new(1, 0, 0);

    /// <summary>The scale held inside the zoom range.</summary>
    /// <param name="scale">A wanted scale.</param>
    /// <returns>The clamped scale.</returns>
    internal static double Clamp(double scale) => throw new NotSupportedException();

    /// <summary>The fit of a world on a stage.</summary>
    /// <param name="stage">The stage's size.</param>
    /// <param name="world">The world's size.</param>
    /// <returns>The view.</returns>
    internal static ZoomView Fit(Size stage, Size world) => throw new NotSupportedException();

    /// <summary>Linear blend of two views.</summary>
    /// <param name="from">The view at 0.</param>
    /// <param name="to">The view at 1.</param>
    /// <param name="t">Progress.</param>
    /// <returns>The view between them.</returns>
    internal static ZoomView Lerp(ZoomView from, ZoomView to, double t) => throw new NotSupportedException();

    /// <summary>The world point under a screen point.</summary>
    /// <param name="screen">A point in the stage.</param>
    /// <returns>The world point.</returns>
    internal Point ToWorld(Point screen) => throw new NotSupportedException();

    /// <summary>The view after zooming around a point.</summary>
    /// <param name="cursor">The stage point that stays put.</param>
    /// <param name="factor">The scale factor.</param>
    /// <returns>The new view.</returns>
    internal ZoomView ZoomAt(Point cursor, double factor) => throw new NotSupportedException();

    /// <summary>The view after a pan.</summary>
    /// <param name="dx">Screen pixels right.</param>
    /// <param name="dy">Screen pixels down.</param>
    /// <returns>The new view.</returns>
    internal ZoomView PanBy(double dx, double dy) => throw new NotSupportedException();

    /// <summary>The view with the world kept partly on the stage.</summary>
    /// <param name="stage">The stage's size.</param>
    /// <param name="world">The world's size.</param>
    /// <returns>The nearest such view.</returns>
    internal ZoomView Kept(Size stage, Size world) => throw new NotSupportedException();

    /// <summary>The smallest pan that brings a world rectangle fully into the stage.</summary>
    /// <param name="target">The rectangle, in world coordinates.</param>
    /// <param name="stage">The stage's size.</param>
    /// <param name="rightInset">Width of the panel over the right edge.</param>
    /// <param name="margin">Space to leave.</param>
    /// <returns>The new view.</returns>
    internal ZoomView Reveal(Rect target, Size stage, double rightInset = 0, double margin = 24) => throw new NotSupportedException();
}
