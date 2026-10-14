using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>The transport a call came through, for the query log (spec §6.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ContextClient>))]
public enum ContextClient
{
    /// <summary>The sindri CLI.</summary>
    [JsonStringEnumMemberName("cli")]
    Cli,

    /// <summary>Plain HTTP (the app, curl).</summary>
    [JsonStringEnumMemberName("http")]
    Http,

    /// <summary>The MCP endpoint.</summary>
    [JsonStringEnumMemberName("mcp")]
    Mcp,

    /// <summary>An in-process service.</summary>
    [JsonStringEnumMemberName("service")]
    Service,
}
