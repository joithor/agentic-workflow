using Aw.Meta.Core;

namespace Aw.Meta.Infrastructure;

/// <summary>Meta's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Meta keeps its state: $AW_STATE_DIR/meta, or ~/.agentic-workflow/meta.</param>
/// <param name="ConfigPath">The team config file: $AW_META_CONFIG, or team.json in the state directory.</param>
/// <param name="SessionsDirectory">Where Claude Code lists live sessions: $AW_CLAUDE_SESSIONS_DIR, or ~/.claude/sessions.</param>
/// <param name="HeavyLock">The heavy-job lock path: $AW_HEAVY_JOB_LOCK, or locks/heavy-job.lock under $AW_STATE_DIR.</param>
/// <param name="LaneSessionsDirectory">What the launcher and the session-id hook record per lane: lane-sessions under the state directory.</param>
/// <param name="Tmux">The tmux program: $AW_TMUX, or tmux.</param>
/// <param name="TmuxSocket">The tmux socket name ($AW_META_TMUX_SOCKET, passed as -L); null means the team's own socket (-L &lt;team&gt;).</param>
/// <param name="Paths">The paths and programs the features use.</param>
internal sealed record MetaSettings(string StateDirectory, string ConfigPath, string SessionsDirectory, string HeavyLock, string LaneSessionsDirectory, string Tmux, string? TmuxSocket, MetaPaths Paths);
