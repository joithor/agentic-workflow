namespace Aw.Meta.Core.Health;

/// <summary>The health check's rules over a process snapshot.</summary>
public static class HostRules
{
    /// <summary>The orphans: processes the orphan rule matches, with their parent looked up in the same snapshot.</summary>
    /// <param name="processes">The snapshot's processes.</param>
    /// <returns>The orphaned processes.</returns>
    public static IReadOnlyList<ProcessInfo> Orphans(IReadOnlyList<ProcessInfo> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        var byPid = processes.ToDictionary(p => p.Pid);
        return [.. processes.Where(p => OrphanRule.IsOrphan(p, byPid.TryGetValue(p.Ppid, out var parent) ? parent.Cmdline : null))];
    }

    /// <summary>MCP server commands running more often than one per Claude session, plus one.</summary>
    /// <param name="processes">The snapshot's processes.</param>
    /// <returns>The commands with too many copies, most copies first.</returns>
    public static IReadOnlyList<DuplicateServer> Duplicates(IReadOnlyList<ProcessInfo> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        var allowed = processes.Count(p => ProcessGroups.Of(p) == ProcessGroups.Claude) + 1;
        return [.. processes
            .Where(p => ProcessGroups.Of(p) == ProcessGroups.Mcp)
            .GroupBy(p => p.Cmdline, StringComparer.Ordinal)
            .Where(group => group.Skip(allowed).Any())
            .Select(group => new DuplicateServer(group.Key, group.Count(), allowed))
            .OrderByDescending(d => d.Copies)];
    }
}
