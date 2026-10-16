using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Which processes are agent sessions (collector spec §4.1 step 2).</summary>
public static class AgentProcessRules
{
    /// <summary>The catalog entry a process matches: argv[0]'s basename, then the exe path part, then comm (cut to 15 characters).</summary>
    /// <param name="process">The process.</param>
    /// <param name="agents">The catalog entries to match.</param>
    /// <returns>The entry, or null.</returns>
    public static AgentEntry? Match(ProcessEntry process, IReadOnlyList<AgentEntry> agents) => throw new NotSupportedException();

    /// <summary>The processes that are sessions: a match that has no ancestor of the same entry, isn't a judge child, and has a terminal unless a record vouches for it.</summary>
    /// <param name="table">The process table.</param>
    /// <param name="agents">The catalog entries to discover.</param>
    /// <param name="isJudgeChild">Whether a process's allowlisted environment has AW_JUDGE_CHILD.</param>
    /// <param name="hasRecord">Whether a Claude record matches the process (pid and start ticks).</param>
    /// <returns>The agent processes.</returns>
    public static IReadOnlyList<AgentProcess> Find(IReadOnlyList<ProcessEntry> table, IReadOnlyList<AgentEntry> agents, Func<int, bool> isJudgeChild, Func<int, bool> hasRecord)
        => throw new NotSupportedException();
}
