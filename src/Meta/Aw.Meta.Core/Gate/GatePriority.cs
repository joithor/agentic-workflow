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

    /// <summary>
    /// The heavy-job wrapper's per-session slot count. The wrapper keys it on the nearest claude ancestor, which for every gate step is the meta
    /// pane, so the default of 1 would run all the steps of all the gates one at a time; aw-meta's own slot pool is the limit instead.
    /// </summary>
    public const string SessionSlots = "AW_SESSION_HEAVY_SLOTS";

    /// <summary>The exit code the heavy-job wrapper gives when it waited too long for a lock (EX_TEMPFAIL).</summary>
    public const int LockTimeout = 75;

    /// <summary>The environment to add to a gate step: the priority, and as many per-session wrapper slots as the pool has heavy slots.</summary>
    /// <param name="heavySlots">The pool's limit.</param>
    /// <returns>The variables.</returns>
    public static IReadOnlyDictionary<string, string> EnvironmentFor(int heavySlots) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Variable] = Meta,
            [SessionSlots] = Math.Max(1, heavySlots).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

    /// <summary>The environment to add to a gate command.</summary>
    public static IReadOnlyDictionary<string, string> Environment { get; } = new Dictionary<string, string>(StringComparer.Ordinal) { [Variable] = Meta };
}
