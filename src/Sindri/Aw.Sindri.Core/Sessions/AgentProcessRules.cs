using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Which processes are agent sessions (collector spec §4.1 step 2).</summary>
public static class AgentProcessRules
{
    private const int CommLength = 15;
    private const int AncestorSteps = 32;
    private const string ClaudeVersions = "/claude/versions/";

    /// <summary>The catalog entry a process matches: argv[0]'s basename, then the exe path part, then 'comm' (cut to 15 characters).</summary>
    /// <param name="process">The process.</param>
    /// <param name="agents">The catalog entries to match.</param>
    /// <returns>The entry, or null.</returns>
    public static AgentEntry? Match(ProcessEntry process, IReadOnlyList<AgentEntry> agents)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(agents);
        return agents.FirstOrDefault(a => Matches(process, a));
    }

    /// <summary>The processes that are sessions: a match that has no ancestor of the same entry, isn't a judge child, and has a terminal unless a record vouches for it.</summary>
    /// <param name="table">The process table.</param>
    /// <param name="agents">The catalog entries to discover.</param>
    /// <param name="isJudgeChild">Whether a process's allowlisted environment has AW_JUDGE_CHILD.</param>
    /// <param name="hasRecord">Whether a Claude record matches the process (pid and start ticks).</param>
    /// <returns>The agent processes.</returns>
    public static IReadOnlyList<AgentProcess> Find(IReadOnlyList<ProcessEntry> table, IReadOnlyList<AgentEntry> agents, Func<int, bool> isJudgeChild, Func<int, bool> hasRecord)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(isJudgeChild);
        ArgumentNullException.ThrowIfNull(hasRecord);
        var byPid = table.GroupBy(static p => p.Pid).ToDictionary(static g => g.Key, static g => g.First());
        var matched = table.Select(p => (Process: p, Agent: Match(p, agents))).Where(static m => m.Agent is not null).ToDictionary(static m => m.Process.Pid, static m => m.Agent!); // why: filtered to non-null
        return [.. matched
            .Where(m => !HasSameAncestor(byPid, matched, byPid[m.Key], m.Value) && !isJudgeChild(m.Key) && (byPid[m.Key].Tty != 0 || hasRecord(m.Key)))
            .Select(m => new AgentProcess(byPid[m.Key], m.Value))
            .OrderBy(static a => a.Process.Pid)];
    }

    private static bool Matches(ProcessEntry process, AgentEntry agent)
    {
        var name = agent.MatchName;
        var named = string.Equals(Base(process.Argv0), name, StringComparison.Ordinal)
            || (agent.Id.Value == "claude-code" && process.ExePath?.Contains(ClaudeVersions, StringComparison.Ordinal) == true)
            || (name.Length <= CommLength && string.Equals(process.Comm, name, StringComparison.Ordinal));
        return named && (agent.Id.Value != "cursor" || process.ExePath?.Contains("cursor", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HasSameAncestor(Dictionary<int, ProcessEntry> byPid, Dictionary<int, AgentEntry> matched, ProcessEntry process, AgentEntry agent)
    {
        var current = process;
        for (var step = 0; step < AncestorSteps && byPid.TryGetValue(current.ParentPid, out var parent) && parent.Pid != current.Pid; step++)
        {
            if (matched.TryGetValue(parent.Pid, out var other) && other.Id.Equals(agent.Id))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static string? Base(string? path)
    {
        if (path is null)
        {
            return null;
        }

        return path[(path.LastIndexOf('/') + 1)..];
    }
}
