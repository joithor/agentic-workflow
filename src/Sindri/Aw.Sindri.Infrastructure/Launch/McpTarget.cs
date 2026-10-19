namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Where a session's MCP calls go: Sindri's loopback port and the repo the session's tools are scoped to.</summary>
/// <param name="Repo">The profile repo name ('X-Sindri-Repo').</param>
/// <param name="Port">Sindri's API port.</param>
internal sealed record McpTarget(string Repo, int Port);
