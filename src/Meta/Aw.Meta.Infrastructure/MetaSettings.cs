using Aw.Meta.Core;

namespace Aw.Meta.Infrastructure;

/// <summary>Meta's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where the selected team keeps its state: $AW_STATE_DIR/meta/teams/&lt;team&gt;, or $AW_STATE_DIR/meta for the legacy flat layout (TeamResolver; ~/.agentic-workflow when AW_STATE_DIR is unset).</param>
/// <param name="ConfigPath">The team config file: $AW_META_CONFIG, or team.json in the state directory.</param>
/// <param name="SessionsDirectory">Where Claude Code lists live sessions: $AW_CLAUDE_SESSIONS_DIR, or ~/.claude/sessions.</param>
/// <param name="HeavyLock">The heavy-job lock path: $AW_HEAVY_JOB_LOCK, or locks/heavy-job.lock under $AW_STATE_DIR.</param>
/// <param name="LaneSessionsDirectory">What the launcher and the session-id hook record per lane: lane-sessions under the state directory.</param>
/// <param name="Tmux">The tmux program: $AW_TMUX, or tmux.</param>
/// <param name="TmuxSocket">The tmux socket name ($AW_META_TMUX_SOCKET, passed as -L); null means the team's own socket (-L &lt;team&gt;).</param>
/// <param name="Paths">The paths and programs the features use.</param>
/// <param name="Layout">Which team this run acts on (AW_META_TEAM) and what else is on disk.</param>
internal sealed record MetaSettings(string StateDirectory, string ConfigPath, string SessionsDirectory, string HeavyLock, string LaneSessionsDirectory, string Tmux, string? TmuxSocket, MetaPaths Paths, TeamLayout Layout);
