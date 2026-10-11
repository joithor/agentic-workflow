using System.Globalization;
using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Geometry;

/// <summary>A browser viewport in CSS pixels.</summary>
/// <param name="W">Width.</param>
/// <param name="H">Height.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ViewportSize(int W, int H)
{
    /// <summary>Writes the size as 'wxh'.</summary>
    /// <returns>The text.</returns>
    public string Describe() => string.Create(CultureInfo.InvariantCulture, $"{W}x{H}");
}
