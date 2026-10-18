using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Where a session's state came from (collector spec §2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StateSource>))]
public enum StateSource
{
    /// <summary>No source: the state is unknown.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Claude's own session record.</summary>
    [JsonStringEnumMemberName("record")]
    Record,

    /// <summary>The aw:fleet hook's state file.</summary>
    [JsonStringEnumMemberName("hook")]
    Hook,

    /// <summary>Only the transcript's modification time: the row says it is estimated.</summary>
    [JsonStringEnumMemberName("transcript")]
    Transcript,
}
