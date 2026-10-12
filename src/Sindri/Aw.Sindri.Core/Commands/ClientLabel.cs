using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Commands;

/// <summary>The client that labelled the request (X-Sindri-Client, API spec §6): it names the actor; written as its lower-case name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClientLabel>))]
public enum ClientLabel
{
    /// <summary>No label, or one Sindri doesn't know: a plain HTTP client ('human:http', 'session:http').</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>The sindri CLI.</summary>
    [JsonStringEnumMemberName("cli")]
    Cli,

    /// <summary>The Sindri app.</summary>
    [JsonStringEnumMemberName("ui")]
    Ui,

    /// <summary>scripts/sindri-prove.sh, honoured only for a temp or proof home.</summary>
    [JsonStringEnumMemberName("prover")]
    Prover,

    /// <summary>The MCP endpoint (always a session).</summary>
    [JsonStringEnumMemberName("mcp")]
    Mcp,
}
