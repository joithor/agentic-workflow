using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A rectangle on the whole map's world, in px (plain numbers: view models carry no Avalonia types; the views convert).</summary>
/// <param name="X">Distance from the left.</param>
/// <param name="Y">Distance from the top.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct WholeRect(double X, double Y, double Width, double Height)
{
    /// <summary>Gets the right edge.</summary>
    internal double Right => X + Width;

    /// <summary>Gets the bottom edge.</summary>
    internal double Bottom => Y + Height;

    /// <summary>Gets the centre's x.</summary>
    internal double CenterX => X + (Width / 2);

    /// <summary>Gets the centre's y.</summary>
    internal double CenterY => Y + (Height / 2);

    /// <summary>Whether two rectangles share any area.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>True when they overlap.</returns>
    internal bool Intersects(WholeRect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;

    /// <summary>Whether a point lies inside.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    /// <returns>True when inside.</returns>
    internal bool Contains(double x, double y) => x >= X && x <= Right && y >= Y && y <= Bottom;
}
