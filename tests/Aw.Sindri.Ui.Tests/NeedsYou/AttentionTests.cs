using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The Needs-you panel's own signals: the header pulses once when rows arrive, Mark all seen offers Undo.</summary>
[Lane(TestLane.Unit)]
public sealed class AttentionTests
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

    private static NeedsYouEntry Pulse(string id) => new(id, NeedsYouUrgency.Pulse);

    [Fact]
    public async Task Panel_header_pulses_once_when_rows_arrive()
    {
        var panel = await Panel();

        panel.Update([Pulse("a"), Pulse("b")]);
        Assert.Equal(1, panel.HeaderPulses); // one pulse for the whole batch

        panel.Update([Pulse("a"), Pulse("b")]);
        Assert.Equal(1, panel.HeaderPulses); // nothing arrived

        panel.Update([Pulse("a")]);
        Assert.Equal(1, panel.HeaderPulses); // a row leaving is not an arrival

        panel.Update([Pulse("a"), Pulse("c")]);
        Assert.Equal(2, panel.HeaderPulses);
    }

    [Fact]
    public async Task Panel_header_does_not_pulse_under_reduced_motion()
    {
        _os.Set(reduce: true);
        var panel = await Panel();

        panel.Update([Pulse("a")]);

        Assert.Equal(0, panel.HeaderPulses);
    }

    [Fact]
    public async Task Mark_all_seen_marks_the_rows_and_names_the_Undo_line()
    {
        var panel = await Panel();
        panel.Update([Pulse("a"), Pulse("b")]);

        panel.MarkAllSeen();

        Assert.All(panel.Rows, r => Assert.False(r.IsPulsing));
        Assert.Equal((true, "2 marked seen"), (panel.UndoOffered, panel.UndoText));

        panel.Undo();
        Assert.All(panel.Rows, r => Assert.True(r.IsPulsing));
        Assert.False(panel.UndoOffered);
    }

    [Fact]
    public async Task Rows_that_arrive_after_Mark_all_seen_are_unseen()
    {
        var panel = await Panel();
        panel.Update([Pulse("a")]);
        panel.MarkAllSeen();

        panel.Update([Pulse("a"), Pulse("b")]);

        Assert.Equal((false, true), (panel.Rows[0].IsPulsing, panel.Rows[1].IsPulsing));
    }

    [Fact]
    public async Task The_Undo_line_goes_after_10_s()
    {
        var panel = await Panel();
        panel.Update([Pulse("a")]);
        panel.MarkAllSeen();

        _clock.Advance(SeenTracker.UndoFor);

        Assert.Equal((false, ""), (panel.UndoOffered, panel.UndoText));
    }
}
