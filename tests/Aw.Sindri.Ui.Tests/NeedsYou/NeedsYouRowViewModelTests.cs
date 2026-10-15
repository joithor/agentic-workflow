using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>How a needs-you row looks by urgency, seen state and motion (UI spec §5; screens spec R8-13: tint, no side stripes).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouRowViewModelTests
{
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeMotionPreference _os = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<NeedsYouSeenViewModel> Panel()
    {
        var panel = new NeedsYouSeenViewModel(new Preferences(_store), _os, _clock);
        await panel.Restore(Token);
        return panel;
    }

    private static NeedsYouEntry Pulse(string id = "p") => new(id, NeedsYouUrgency.Pulse);

    [Fact]
    public async Task Pulse_row_pulses_until_seen_then_the_tint_clears()
    {
        var panel = await Panel();
        panel.Update([Pulse()]);
        var row = panel.Rows[0];
        Assert.Equal((RowTint.Pulsing, true, false, ""), (row.Tint, row.IsPulsing, row.HasOutline, row.NewWord));

        panel.Acted("p");

        Assert.Equal((RowTint.None, false, false, ""), (row.Tint, row.IsPulsing, row.HasOutline, row.NewWord));
    }

    [Fact]
    public async Task Colour_row_has_a_steady_tint_and_no_motion()
    {
        var panel = await Panel();
        panel.Update([new NeedsYouEntry("c", NeedsYouUrgency.Colour)]);

        var row = panel.Rows[0];
        Assert.Equal((RowTint.Steady, false, false), (row.Tint, row.IsPulsing, row.HasOutline));

        panel.MarkAllSeen();
        Assert.Equal(RowTint.Steady, row.Tint);
    }

    [Fact]
    public async Task Calm_row_has_a_subtle_tint()
    {
        var panel = await Panel();
        panel.Update([new NeedsYouEntry("k", NeedsYouUrgency.Calm)]);

        Assert.Equal((RowTint.Subtle, false, false), (panel.Rows[0].Tint, panel.Rows[0].IsPulsing, panel.Rows[0].HasOutline));
    }

    [Fact]
    public async Task Reduced_motion_replaces_the_pulse_with_an_outline_and_the_word_new()
    {
        _os.Set(reduce: true);
        var panel = await Panel();
        panel.Update([Pulse()]);
        var row = panel.Rows[0];

        Assert.Equal((RowTint.Steady, false, true, "new"), (row.Tint, row.IsPulsing, row.HasOutline, row.NewWord));

        panel.Acted("p");
        Assert.Equal((false, ""), (row.HasOutline, row.NewWord));
    }

    [Fact]
    public async Task The_in_app_switch_forces_reduced_motion_whatever_the_OS_says()
    {
        var panel = await Panel();
        panel.Update([Pulse()]);
        Assert.False(panel.ReducedMotion);

        panel.SetInAppReduceMotion(reduce: true);

        Assert.True(panel.ReducedMotion);
        Assert.Equal((false, true, "new"), (panel.Rows[0].IsPulsing, panel.Rows[0].HasOutline, panel.Rows[0].NewWord));
    }

    [Fact]
    public async Task The_OS_setting_changing_updates_the_rows()
    {
        var panel = await Panel();
        panel.Update([Pulse()]);

        _os.Set(reduce: true);

        Assert.True(panel.ReducedMotion);
        Assert.True(panel.Rows[0].HasOutline);
    }

    [Fact]
    public async Task New_rows_slide_in_unless_reduced()
    {
        var panel = await Panel();
        panel.Update([Pulse("a")]);
        Assert.False(panel.Rows[0].SlidesIn); // the first list is not an arrival

        panel.Update([Pulse("a"), Pulse("b")]);
        Assert.Equal((false, true), (panel.Rows[0].SlidesIn, panel.Rows[1].SlidesIn));

        _os.Set(reduce: true);
        panel.Update([Pulse("a"), Pulse("b"), Pulse("c")]);
        Assert.False(panel.Rows[2].SlidesIn);
    }
}
