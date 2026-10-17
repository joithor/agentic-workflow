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
        "TMUX", "TMUX_PANE", "AW_META_TEAM", "AW_META_DIR", "AW_META_CONFIG", "AW_META_LANE", "AW_META_LANE_PID",
        "AW_META_LANE_AGENT", "AW_META_LANE_PROC", "AW_SINDRI_CHILD",
    ];

    /// <summary>The environment to add to a gate command.</summary>
    public static IReadOnlyDictionary<string, string> Environment { get; } = new Dictionary<string, string>(StringComparer.Ordinal) { [Variable] = Meta };
}
