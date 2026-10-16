using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What <see cref="LauncherStartup.Load"/> found.</summary>
/// <param name="Catalog">The merged agent catalog.</param>
/// <param name="Sessions">The collector's settings.</param>
/// <param name="ClaudeSessionsDirectory">Where Claude Code's session records are.</param>
internal sealed record LauncherLoad(AgentCatalog Catalog, SessionSettings Sessions, string ClaudeSessionsDirectory);
