using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Why a session is asking (collector spec §2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AskingReason>))]
public enum AskingReason
{
    /// <summary>A tool needs the human's approval.</summary>
    [JsonStringEnumMemberName("approval")]
    Approval,

    /// <summary>The agent asked a question.</summary>
    [JsonStringEnumMemberName("input")]
    Input,

    /// <summary>The agent needs a login.</summary>
    [JsonStringEnumMemberName("auth")]
    Auth,
}
