namespace Aw.Meta.Core.Gate;

/// <summary>The environment every gate command runs with: the heavy-job wrapper lets a job with AW_HEAVY_PRIORITY=meta use any box-wide slot.</summary>
public static class GatePriority
{
    /// <summary>The variable the wrapper reads.</summary>
    public const string Variable = "AW_HEAVY_PRIORITY";

    /// <summary>The value that marks a gate job.</summary>
    public const string Meta = "meta";

    /// <summary>
    /// The variables a gate command must not inherit: the meta pane's tmux and team selection (aw-meta resolves its team itself) and the
    /// lane and Sindri session markers. Tests that exercise the session hooks fail when these leak in.
    /// </summary>
    public static IReadOnlyList<string> Scrubbed { get; } =
    [
        "TMUX", "TMUX_PANE", "AW_META_TEAM", "AW_META_DIR", "AW_META_LANE", "AW_META_LANE_PID", "AW_META_LANE_AGENT", "AW_META_LANE_PROC",
        "AW_SINDRI_CHILD", "AW_SINDRI_LAUNCH", "AW_LAUNCHER_CONFIG",
    ];

    /// <summary>The hook and provider overrides the install helpers honour: a caller's must not steer a suite at real files.</summary>
    public static IReadOnlyList<string> HookOverrides { get; } =
    [
        "CLAUDE_SETTINGS_FILE", "CLAUDE_HOOKS_DIR", "CLAUDE_DIR", "CODEX_HOOKS_FILE", "CODEX_HOME", "CURSOR_HOOKS_FILE", "AW_HOOKS_DIR",
        "AW_HOOK_ADAPTER_FLAGS", "AW_META_CONFIG", "AW_SINDRI_HOME", "CLAUDE_LOCAL_BIN",
    ];

    /// <summary>Everything an isolated suite run strips, as scripts/ci/bash-tests.sh --run-one does: <see cref="HookOverrides"/> and <see cref="Scrubbed"/>.</summary>
    public static IReadOnlyList<string> SuiteUnset { get; } = [.. HookOverrides, .. Scrubbed];

    /// <summary>The environment to add to a gate command.</summary>
    public static IReadOnlyDictionary<string, string> Environment { get; } = new Dictionary<string, string>(StringComparer.Ordinal) { [Variable] = Meta };
}
