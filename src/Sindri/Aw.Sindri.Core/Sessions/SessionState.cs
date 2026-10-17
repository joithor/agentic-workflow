using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>What a session is doing (collector spec §2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SessionState>))]
public enum SessionState
{
    /// <summary>The state can't be told.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,

    /// <summary>A turn is running.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>The agent waits on the human: an approval, a question or a login.</summary>
    [JsonStringEnumMemberName("asking")]
    Asking,

    /// <summary>The turn ended; it is the human's turn.</summary>
    [JsonStringEnumMemberName("idle")]
    Idle,
}
