using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Commands;

/// <summary>Who is calling (API spec §4.2); written as its lower-case name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CallerClass>))]
public enum CallerClass
{
    /// <summary>The owner: the CLI at a terminal with no agent signal, or the app (X-Sindri-Caller: human with the per-start token).</summary>
    [JsonStringEnumMemberName("human")]
    Human,

    /// <summary>An agent session, a script or a fetch tool: the safe default when the header is missing.</summary>
    [JsonStringEnumMemberName("session")]
    Session,

    /// <summary>An in-process caller (the host, the scheduler, the engine), never over HTTP.</summary>
    [JsonStringEnumMemberName("service")]
    Service,
}
