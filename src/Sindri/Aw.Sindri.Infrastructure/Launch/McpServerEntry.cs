namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One server in Claude Code's MCP config file: Sindri's endpoint with the repo header and nothing secret.</summary>
/// <param name="Type">The transport ('http').</param>
/// <param name="Url">The loopback endpoint.</param>
/// <param name="Headers">The repo header the endpoint scopes by.</param>
internal sealed record McpServerEntry(string Type, string Url, IReadOnlyDictionary<string, string> Headers);
