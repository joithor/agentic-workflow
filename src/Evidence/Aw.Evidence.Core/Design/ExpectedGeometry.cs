using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Design;

/// <summary>The size and position an element should have; at least one axis is set.</summary>
/// <param name="W">Expected width.</param>
/// <param name="H">Expected height.</param>
/// <param name="X">Expected left edge (page, or relative to another element).</param>
/// <param name="Y">Expected top edge (page, or relative to another element).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExpectedGeometry(double? W = null, double? H = null, double? X = null, double? Y = null)
{
    /// <summary>True when no axis is set.</summary>
    [JsonIgnore]
    public bool IsEmpty => W is null && H is null && X is null && Y is null;
}
