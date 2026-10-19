using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>Whether a section has rows, none, or cannot have any yet.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScopingSectionState>))]
public enum ScopingSectionState
{
    /// <summary>It has rows.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready = 1,

    /// <summary>Its facts exist and hold nothing; the note says so.</summary>
    [JsonStringEnumMemberName("empty")]
    Empty = 2,

    /// <summary>The state that writes its facts has not run; the note names it.</summary>
    [JsonStringEnumMemberName("not-yet")]
    NotYet = 3,
}
