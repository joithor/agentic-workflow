using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>A point on the map's world, in px (plain numbers: view models carry no Avalonia types; the views convert).</summary>
/// <param name="X">Distance from the left.</param>
/// <param name="Y">Distance from the top.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct MapPoint(double X, double Y)
{
    /// <summary>Moves a point by a vector.</summary>
    /// <param name="point">The point.</param>
    /// <param name="vector">The move.</param>
    /// <returns>The moved point.</returns>
    public static MapPoint operator +(MapPoint point, MapVector vector) => Add(point, vector);

    /// <summary>Moves a point back by a vector.</summary>
    /// <param name="point">The point.</param>
    /// <param name="vector">The move.</param>
    /// <returns>The moved point.</returns>
    public static MapPoint operator -(MapPoint point, MapVector vector) => new(point.X - vector.X, point.Y - vector.Y);

    /// <summary>The vector from one point to another.</summary>
    /// <param name="to">The end.</param>
    /// <param name="from">The start.</param>
    /// <returns>The vector that leads from <paramref name="from"/> to <paramref name="to"/>.</returns>
    public static MapVector operator -(MapPoint to, MapPoint from) => Subtract(to, from);

    /// <summary>Moves a point by a vector.</summary>
    /// <param name="point">The point.</param>
    /// <param name="vector">The move.</param>
    /// <returns>The moved point.</returns>
    public static MapPoint Add(MapPoint point, MapVector vector) => new(point.X + vector.X, point.Y + vector.Y);

    /// <summary>The vector from one point to another.</summary>
    /// <param name="to">The end.</param>
    /// <param name="from">The start.</param>
    /// <returns>The vector that leads from <paramref name="from"/> to <paramref name="to"/>.</returns>
    public static MapVector Subtract(MapPoint to, MapPoint from) => new(to.X - from.X, to.Y - from.Y);
}
