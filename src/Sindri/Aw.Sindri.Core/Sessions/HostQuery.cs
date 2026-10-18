namespace Aw.Sindri.Core.Sessions;

/// <summary>One session's host question: the process table, the agent, its allowlisted environment and what its record says.</summary>
/// <param name="Table">The process table by pid.</param>
/// <param name="Agent">The agent process.</param>
/// <param name="AgentEnvironment">The agent's allowlisted environment.</param>
public sealed record HostQuery(IReadOnlyDictionary<int, ProcessEntry> Table, ProcessEntry Agent, IReadOnlyDictionary<string, string> AgentEnvironment)
{
    /// <summary>The Claude record's tmux field, '&lt;session&gt;:@&lt;window&gt;.%&lt;pane&gt;'.</summary>
    public string? RecordTmux { get; init; }

    /// <summary>True for a T3 Code session (an sdk-ts record under .t3/worktrees).</summary>
    public bool T3 { get; init; }
}
