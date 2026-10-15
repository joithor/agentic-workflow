namespace Aw.Sindri.Host.Mcp;

/// <summary>The Sindri instance that answered an MCP call (owner checks confirm it is the scratch one).</summary>
/// <param name="Build">The host's informational version.</param>
/// <param name="Port">The port the call arrived on.</param>
internal sealed record McpInstance(string Build, int Port);
