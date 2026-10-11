using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Images;

/// <summary>The result of comparing two same-size images.</summary>
/// <param name="DiffPixels">Pixels that differ beyond the threshold (anti-aliased ones only when included).</param>
/// <param name="Total">Pixels compared.</param>
/// <param name="Diff">A visual diff: unchanged pixels faded grey, differing ones red, ignored anti-aliased ones yellow.</param>
/// <param name="Changed">The bounding box of the differing pixels, or null when none differ.</param>
public sealed record PixelComparison(long DiffPixels, long Total, RgbaImage Diff, Region? Changed)
{
    /// <summary>The fraction of differing pixels, 0..1.</summary>
    public double DiffScore => Total == 0 ? 0 : (double)DiffPixels / Total;

    /// <summary>The percentage of differing pixels, rounded to two decimals.</summary>
    public double DiffPercent => Math.Round(DiffScore * 100, 2, MidpointRounding.AwayFromZero);
}
