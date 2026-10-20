namespace Aw.Sindri.Core.Setup;

/// <summary>Where the per-repo MCP registration stands.</summary>
/// <param name="Repos">Repos the approved profile names (0 until a profile with a repo is approved).</param>
/// <param name="Registered">Of those, the ones whose local registration exists.</param>
public sealed record McpFact(int Repos, int Registered);
