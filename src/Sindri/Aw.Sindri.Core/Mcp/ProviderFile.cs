using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Mcp;

/// <summary>A provider's user-wide MCP configuration file (context spec §3: doctor context:mcp-scope).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProviderFile>))]
public enum ProviderFile
{
    /// <summary>Claude Code: ~/.claude.json ($CLAUDE_CONFIG_DIR/.claude.json when set); user scope is its top-level mcpServers.</summary>
    [JsonStringEnumMemberName("claude")]
    Claude,

    /// <summary>Codex: ~/.codex/config.toml ($CODEX_HOME/config.toml when set); its [mcp_servers.*] tables.</summary>
    [JsonStringEnumMemberName("codex")]
    Codex,

    /// <summary>Cursor: ~/.cursor/mcp.json; its mcpServers.</summary>
    [JsonStringEnumMemberName("cursor")]
    Cursor,
}
