using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>How a lens item relates to the anchor.</summary>
[JsonConverter(typeof(StrictEnumConverter<LensRelation>))]
public enum LensRelation
{
    /// <summary>In the anchor's own scope (its descendants, their surfaces and actions).</summary>
    [JsonStringEnumMemberName("own")]
    Own,

    /// <summary>One edge away from the scope.</summary>
    [JsonStringEnumMemberName("neighbor")]
    Neighbor,

    /// <summary>Reached by the cascade: something the anchor affects.</summary>
    [JsonStringEnumMemberName("affects")]
    Affects,
}
