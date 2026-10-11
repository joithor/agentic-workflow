using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Visual;

/// <summary>A screenshot compared with its baseline.</summary>
/// <param name="Score">Fraction of differing pixels; 1 when the sizes differ.</param>
/// <param name="Changed">The differing pixels' bounding box; null when none differ or the sizes differ.</param>
/// <param name="DiffImage">The written diff overlay; null when the sizes differ.</param>
internal sealed record BaselineDiff(double Score, Region? Changed, string? DiffImage)
{
    public bool IsChanged => Score > 0;
}
