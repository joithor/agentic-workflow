using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Commands;

/// <summary>How a command attempt ended, as its 'command.handled' audit row records it (API spec §3.4); written as its lower-case name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CommandOutcome>))]
public enum CommandOutcome
{
    /// <summary>The handler ran and succeeded.</summary>
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    /// <summary>The caller may not run its action class (SND-AUTH-*).</summary>
    [JsonStringEnumMemberName("refused")]
    Refused,

    /// <summary>The validator or the handler refused the input (an Invalid error).</summary>
    [JsonStringEnumMemberName("invalid")]
    Invalid,

    /// <summary>The handler returned any other error.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
