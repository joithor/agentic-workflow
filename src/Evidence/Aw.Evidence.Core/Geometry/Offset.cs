using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Geometry;

/// <summary>A point or displacement in CSS pixels.</summary>
/// <param name="X">Horizontal component.</param>
/// <param name="Y">Vertical component.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record Offset(double X, double Y)
{
    /// <summary>No displacement.</summary>
    public static Offset Zero { get; } = new(0, 0);
}
