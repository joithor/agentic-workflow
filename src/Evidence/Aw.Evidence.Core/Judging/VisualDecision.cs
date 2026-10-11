using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Judging;

/// <summary>A visual critique's verdict.</summary>
[JsonConverter(typeof(StrictEnumConverter<VisualDecision>))]
public enum VisualDecision
{
    /// <summary>The screenshot looks right.</summary>
    [JsonStringEnumMemberName("looks-right")]
    LooksRight,

    /// <summary>Something looks wrong.</summary>
    [JsonStringEnumMemberName("looks-off")]
    LooksOff,

    /// <summary>It works but looks careless.</summary>
    [JsonStringEnumMemberName("sloppy")]
    Sloppy,
}
