using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Whether an agent Step's session is still running, as the watch sees it (engine P8 D3): live, or ended by an exit or a lost heartbeat.</summary>
/// <param name="EndedAs">How the session ended, or null while it runs.</param>
/// <param name="ExitCode">The agent's exit code for an exit, otherwise null.</param>
public sealed record AgentSessionState(SessionEndReason? EndedAs = null, int? ExitCode = null)
{
    /// <summary>The session has not ended.</summary>
    public static AgentSessionState Live { get; } = new();

    /// <summary>Whether the session is still running.</summary>
    public bool IsLive => EndedAs is null;
}
