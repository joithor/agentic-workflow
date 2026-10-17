using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What launcher.json and the built-ins give the collector.</summary>
/// <param name="Agents">The merged catalog, built-ins first.</param>
/// <param name="Sessions">The 'sessions' section, or null.</param>
internal sealed record LauncherConfig(IReadOnlyList<AgentEntry> Agents, SessionsSection? Sessions)
{
    /// <summary>What was wrong in the parts the collector reads (the file as a whole, 'agents' entries, 'sessions'): the sentence behind the launcher problem row. Null when all was well.</summary>
    public string? Problem { get; init; }

    /// <summary>What was wrong in 'viewer' or 'host': it only affects attended opens, so the collector never sees it. Each entry names its section.</summary>
    public IReadOnlyList<string> AttendedProblems { get; init; } = [];
}
