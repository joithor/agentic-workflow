namespace Aw.Sindri.Core.Sessions;

/// <summary>The only environment names the collector keeps (collector spec §4.1 step 5); every other value is dropped as it is parsed and never logged.</summary>
public static class EnvironmentAllowlist
{
    /// <summary>The names.</summary>
    public static IReadOnlySet<string> Names { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "AW_SINDRI_LAUNCH", "AW_SINDRI_CHILD", "AW_JUDGE_CHILD", "AW_META_TEAM", "AW_META_LANE", "TMUX", "TMUX_PANE", "TERM_PROGRAM", "WARP_FOCUS_URL",
        "KITTY_WINDOW_ID", "KITTY_LISTEN_ON", "WEZTERM_PANE", "WINDOWID", "DISPLAY", "WAYLAND_DISPLAY", "XDG_SESSION_TYPE", "DBUS_SESSION_BUS_ADDRESS",
    };

    /// <summary>Keeps the allowlisted entries.</summary>
    /// <param name="all">The parsed name and value pairs.</param>
    /// <returns>Only the allowlisted names.</returns>
    public static IReadOnlyDictionary<string, string> Filter(IEnumerable<KeyValuePair<string, string>> all)
    {
        ArgumentNullException.ThrowIfNull(all);
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in all)
        {
            if (Names.Contains(name))
            {
                kept[name] = value;
            }
        }

        return kept;
    }
}
