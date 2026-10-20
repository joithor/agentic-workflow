using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'mcp.registered' payload (setup-once spec §7.5): Sindri registered itself in a repo. Key 'mcp.registered:&lt;commandId&gt;'.</summary>
/// <param name="CommandId">The 'RegisterMcp' command's id.</param>
/// <param name="Repo">The repo.</param>
/// <param name="Port">The port the registered address names.</param>
/// <param name="Providers">One outcome per provider.</param>
public sealed record McpRegistered(CommandId CommandId, RepoName Repo, int Port, IReadOnlyList<McpProviderOutcome> Providers);
