using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Design;

/// <summary>Where to cut the implementation screenshot, and whether the viewport can show it.</summary>
public static class ParityRegions
{
    /// <summary>The anchor's origin plus <paramref name="offset"/> (rounded to whole pixels), at the design region's size.</summary>
    /// <param name="anchor">The anchor's measured box.</param>
    /// <param name="design">The design region.</param>
    /// <param name="offset">The anchor offset.</param>
    /// <returns>The implementation region.</returns>
    public static Region Implementation(Box anchor, Region design, Offset offset) =>
        new(Round(anchor.X + offset.X), Round(anchor.Y + offset.Y), design.W, design.H);

    /// <summary>The viewport needed to capture <paramref name="region"/> in full, or null when it already fits.</summary>
    /// <param name="region">The region.</param>
    /// <param name="viewport">The viewport.</param>
    /// <returns>The needed viewport, or null.</returns>
    public static ViewportSize? Shortfall(Region region, ViewportSize viewport) =>
        region.Right > viewport.W || region.Bottom > viewport.H
            ? new ViewportSize(Math.Max(region.Right, viewport.W), Math.Max(region.Bottom, viewport.H))
            : null;

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
