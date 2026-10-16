using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Only the allowlisted environment names survive (collector spec §4.1 step 5, §12).</summary>
public sealed class EnvironmentAllowlistTests
{
    [Fact, Trait("Lane", "unit")]
    public void Environ_drops_every_name_off_the_allowlist()
    {
        const string Planted = "sk-ant-FAKE-must-never-appear";
        KeyValuePair<string, string>[] all =
        [
            new("ANTHROPIC_API_KEY", Planted), new("HOME", "/h"), new("PATH", "/usr/bin"), new("AW_SINDRI_LAUNCH", "LCH-00000000000000000000000000000001"), new("TMUX", "/tmp/tmux-1000/t1,400,0"),
            new("WARP_FOCUS_URL", "warp://session/0123456789abcdef0123456789abcdef"), new("DISPLAY", ":0"), new("GITHUB_TOKEN", Planted),
        ];

        var kept = EnvironmentAllowlist.Filter(all);

        Assert.Equal(["AW_SINDRI_LAUNCH", "DISPLAY", "TMUX", "WARP_FOCUS_URL"], kept.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.DoesNotContain(Planted, kept.Values, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_allowlist_is_exactly_the_spec_names() =>
        Assert.Equal(
            ["AW_JUDGE_CHILD", "AW_META_LANE", "AW_META_TEAM", "AW_SINDRI_CHILD", "AW_SINDRI_LAUNCH", "DBUS_SESSION_BUS_ADDRESS", "DISPLAY", "KITTY_LISTEN_ON", "KITTY_WINDOW_ID",
                "TERM_PROGRAM", "TMUX", "TMUX_PANE", "WARP_FOCUS_URL", "WAYLAND_DISPLAY", "WEZTERM_PANE", "WINDOWID", "XDG_SESSION_TYPE"],
            EnvironmentAllowlist.Names.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
}
