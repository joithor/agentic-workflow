using Aw.Meta.Core.Usage;

namespace Aw.Meta.Tests.Usage;

public sealed class UsagePaneTests
{
    private const string Stop = "  ⎿  You've hit your session limit · resets 2:50am (America/Los_Angeles)";
    private static readonly string _rule = new('─', 40);
    private static readonly DateTimeOffset _reference = new(2026, 10, 18, 20, 0, 0, TimeSpan.Zero);

    private static PaneUsage Read(string capture) => UsagePane.Read(capture, _reference, TimeZoneInfo.Utc);

    private static string Screen(string before, string prompt = "❯ ", string between = "") =>
        $"{before}\n{between}\n{_rule}\n{prompt}\n{_rule}\n  ⏵⏵ bypass permissions on (shift+tab to cycle)\n";

    [Fact, Trait("Lane", "unit")]
    public void A_stop_followed_by_an_empty_prompt_is_current_and_idle()
    {
        var pane = Read(Screen("⏺ Working on it.\n" + Stop));

        Assert.NotNull(pane.Stop);
        Assert.True(pane.Current);
        Assert.True(pane.Idle);
        Assert.Equal(UsageKind.Session, pane.Stop.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_placeholder_Claude_shows_in_an_empty_box_still_counts_as_empty() =>
        Assert.True(Read(Screen(Stop, "❯ Try \"fix the failing test\"")).Idle);

    [Fact, Trait("Lane", "unit")]
    public void Work_running_makes_the_lane_busy_even_when_the_stop_is_on_screen()
    {
        var pane = Read(Screen(Stop, between: "✻ Thinking… (12s · esc to interrupt)"));

        Assert.True(pane.Current);
        Assert.False(pane.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_in_the_input_box_is_never_idle()
    {
        var pane = Read(Screen(Stop, "❯ half a sentence the owner is typing"));

        Assert.True(pane.Current);
        Assert.False(pane.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public void Output_after_the_stop_means_the_lane_went_on_and_the_stop_is_no_longer_current()
    {
        var pane = Read(Screen(Stop + "\n> continue\n⏺ Resumed, running the tests."));

        Assert.NotNull(pane.Stop);
        Assert.False(pane.Current);
        Assert.False(pane.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_screen_without_a_stop_has_none()
    {
        var pane = Read(Screen("⏺ All done."));

        Assert.Null(pane.Stop);
        Assert.False(pane.Current);
        Assert.False(pane.Idle);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_last_stop_on_the_screen_is_the_one_read()
    {
        var capture = Screen("You've hit your session limit · resets 1am (UTC)\n⏺ later\nYou've hit your weekly limit · resets Oct 20, 3am (UTC)");

        Assert.Equal(UsageKind.Weekly, Read(capture).Stop?.Kind);
    }
}
