using Avalonia;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The path of one edge on the context map (F3 spec 1 §2, "Edges"): where it starts, then lines, arcs and cubics. Edges inside
/// a band are rounded elbows (arcs of radius <see cref="ElbowRadius"/>); edges between bands are S-curves. Built from fixed
/// coordinates, so the geometry is tested without drawing it.
/// </summary>
/// <param name="Start">The first point (a port on the source node).</param>
/// <param name="Steps">The steps to the last point (a port on the target node).</param>
internal sealed record EdgeRoute(Point Start, IReadOnlyList<RouteStep> Steps)
{
    /// <summary>The radius of an elbow's corner, px.</summary>
    internal const double ElbowRadius = 12;

    /// <summary>Gets the last point.</summary>
    internal Point End => Steps.Count == 0 ? Start : Steps[^1].To;

    /// <summary>A straight line.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Straight(Point from, Point to) => throw new NotSupportedException();

    /// <summary>The S-curve between two bands.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Curve(Point from, Point to) => throw new NotSupportedException();

    /// <summary>A line through corner points with each corner rounded by an arc.</summary>
    /// <param name="points">The start, the corners and the end.</param>
    /// <param name="radius">The corner radius.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Elbow(IReadOnlyList<Point> points, double radius = ElbowRadius) => throw new NotSupportedException();

    /// <summary>The rounded three-leg route between two points of one band.</summary>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    /// <param name="runY">The y of the horizontal leg.</param>
    /// <returns>The route.</returns>
    internal static EdgeRoute Bus(Point from, Point to, double runY) => throw new NotSupportedException();

    /// <summary>The point half way along the route.</summary>
    /// <returns>The midpoint.</returns>
    internal Point Midpoint() => throw new NotSupportedException();

    /// <summary>The route as a geometry to stroke.</summary>
    /// <returns>An open stream geometry.</returns>
    internal StreamGeometry ToGeometry() => throw new NotSupportedException();
}
