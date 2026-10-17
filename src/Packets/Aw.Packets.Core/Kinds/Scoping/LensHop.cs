using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Scoping;

/// <summary>A hop kind a lens walked (context spec C4/C6: the lens result's searched.hops).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LensHop>))]
public enum LensHop
{
    /// <summary>A state hop.</summary>
    [JsonStringEnumMemberName("state")]
    State = 1,

    /// <summary>A flow hop.</summary>
    [JsonStringEnumMemberName("flow")]
    Flow = 2,

    /// <summary>A dependency hop.</summary>
    [JsonStringEnumMemberName("dependency")]
    Dependency = 3,

    /// <summary>The opt-in component hop (S-CH).</summary>
    [JsonStringEnumMemberName("component")]
    Component = 4,
}
