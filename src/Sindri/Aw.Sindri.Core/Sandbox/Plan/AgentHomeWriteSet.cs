namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The closed set of entries inside an agent home that a session may write (sandbox spec §3.1 item 2; spike item 2). Everything else in the owner's home stays read-only.</summary>
public static class AgentHomeWriteSet
{
    /// <summary>Whether a session may write <paramref name="relative"/> inside <paramref name="agentHome"/> ('.../.claude' or '.../.codex').</summary>
    /// <param name="agentHome">The agent home path.</param>
    /// <param name="relative">The path inside it; only its first segment counts.</param>
    /// <returns>True when that entry is in the write set.</returns>
    public static bool Allows(string agentHome, string relative) => throw new NotSupportedException();
}
