namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The file behind the 'claude-config-file' MCP form (<c lang="text">--mcp-config</c>).</summary>
/// <param name="McpServers">The servers, by name.</param>
internal sealed record McpConfigDocument(IReadOnlyDictionary<string, McpServerEntry> McpServers);
