using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What launcher.json and the built-ins give the collector.</summary>
/// <param name="Agents">The merged catalog, built-ins first.</param>
/// <param name="Sessions">The 'sessions' section, or null.</param>
internal sealed record LauncherConfig(IReadOnlyList<AgentEntry> Agents, SessionsSection? Sessions);
