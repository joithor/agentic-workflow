using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>A rectangle on the map's world, in px.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct MapRect(double X, double Y, double Width, double Height)
{
    /// <summary>Gets the left edge.</summary>
    internal double Left => X;

    /// <summary>Gets the right edge.</summary>
    internal double Right => X + Width;

    /// <summary>Gets the top edge.</summary>
    internal double Top => Y;

    /// <summary>Gets the bottom edge.</summary>
    internal double Bottom => Y + Height;

    /// <summary>Gets the middle.</summary>
    internal MapPoint Center => new(X + (Width / 2), Y + (Height / 2));

    /// <summary>The rectangle moved by a vector.</summary>
    /// <param name="vector">The move.</param>
    /// <returns>The moved rectangle.</returns>
    internal MapRect Translate(MapVector vector) => new(X + vector.X, Y + vector.Y, Width, Height);

    /// <summary>Whether two rectangles overlap (touching edges do not).</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>True when they share area.</returns>
    internal bool Intersects(MapRect other) => X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}
