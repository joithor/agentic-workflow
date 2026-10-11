using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Geometry;

/// <summary>An element's measured bounding box in CSS pixels (fractional, rounded to two decimals by the browser adapter).</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="W">Width.</param>
/// <param name="H">Height.</param>
public sealed record Box(double X, double Y, double W, double H)
{
    /// <summary>The right edge.</summary>
    [JsonIgnore]
    public double Right => X + W;

    /// <summary>True when the two boxes share any area (touching edges don't count).</summary>
    /// <param name="other">The other box.</param>
    /// <returns>Whether they overlap.</returns>
    public bool Overlaps(Box other) => X < other.X + other.W && X + W > other.X && Y < other.Y + other.H && Y + H > other.Y;
}
