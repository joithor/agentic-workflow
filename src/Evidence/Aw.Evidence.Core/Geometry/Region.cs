using System.Globalization;
using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Geometry;

/// <summary>A whole-pixel rectangle inside an image (a design region, a crop, a diff box).</summary>
/// <param name="X">Left edge, in pixels.</param>
/// <param name="Y">Top edge, in pixels.</param>
/// <param name="W">Width, in pixels.</param>
/// <param name="H">Height, in pixels.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Region(int X, int Y, int W, int H)
{
    /// <summary>The right edge (exclusive).</summary>
    [JsonIgnore]
    public int Right => X + W;

    /// <summary>The bottom edge (exclusive).</summary>
    [JsonIgnore]
    public int Bottom => Y + H;

    /// <summary>True when the region is non-empty and wholly inside a <paramref name="width"/> x <paramref name="height"/> image.</summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>Whether it fits.</returns>
    public bool FitsIn(int width, int height) => W > 0 && H > 0 && X >= 0 && Y >= 0 && Right <= width && Bottom <= height;

    /// <summary>Writes the region as 'x,y wxh'.</summary>
    /// <returns>The text.</returns>
    public string Describe() => string.Create(CultureInfo.InvariantCulture, $"{X},{Y} {W}x{H}");
}
