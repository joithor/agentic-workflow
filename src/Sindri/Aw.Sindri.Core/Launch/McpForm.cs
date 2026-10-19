using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>How a catalog entry is told where Sindri's MCP endpoint is at launch (launcher spec section 3, the entry's 'mcp' field).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpForm>))]
public enum McpForm
{
    /// <summary>The CLI is given no endpoint (Cursor is registered by the owner, never per launch).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>'--mcp-config &lt;file&gt;' to a 0600 per-launch file that names the endpoint and the repo header and holds no token.</summary>
    [JsonStringEnumMemberName("claude-config-file")]
    ClaudeConfigFile,

    /// <summary>Typed '-c mcp_servers.sindri.*' overrides: the endpoint and the repo header, no file and no token.</summary>
    [JsonStringEnumMemberName("codex-overrides")]
    CodexOverrides,
}
