using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The tracker's timers fire on a pool thread (review run 1, B7, K3/test-2): what they change reaches the bound properties on the UI thread.</summary>
[Lane(TestLane.Unit)]
public sealed class SeenTrackerThreadTests
{
    private readonly OffThreadTimers _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<List<int>> Raised(Func<SeenTracker, Task> act, OffThreadTimers clock)
    {
        var tracker = new SeenTracker(new Preferences(new InMemoryPreferencesStore()), clock);
        var threads = new List<int>();
        tracker.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
        await act(tracker);
        await Task.Yield();
        return threads;
    }

    [Fact]
    public Task The_Undo_expiry_reports_its_change_on_the_UI_thread() => UiThread.Run(async () =>
    {
        var ui = Environment.CurrentManagedThreadId;

        var threads = await Raised(async tracker =>
        {
            tracker.MarkAllSeen(["a", "b"]);
            _clock.Advance(SeenTracker.UndoFor);
            await Task.Yield();
            Assert.Equal(0, tracker.UndoCount);
        }, _clock);

        Assert.NotEmpty(threads);
        Assert.All(threads, t => Assert.Equal(ui, t));
    }, Token);

    [Fact]
    public Task The_dwell_timer_reports_a_row_seen_on_the_UI_thread() => UiThread.Run(async () =>
    {
        var ui = Environment.CurrentManagedThreadId;

        var threads = await Raised(async tracker =>
        {
            tracker.Watch("a", onScreenAndFocused: true);
            _clock.Advance(SeenTracker.OnScreenFor);
            await Task.Yield();
            Assert.True(tracker.IsSeen("a"));
        }, _clock);

        Assert.NotEmpty(threads);
        Assert.All(threads, t => Assert.Equal(ui, t));
    }, Token);

    // Review run 2 (test-r2-1, L10): a callback posted to the UI queue runs a dispatcher turn later; what happened in between wins.
    [Fact]
    public Task A_posted_expiry_does_not_end_a_newer_offer() => UiThread.Run(async () =>
    {
        var tracker = new SeenTracker(new Preferences(new InMemoryPreferencesStore()), _clock);
        tracker.MarkAllSeen(["a"]);
        _clock.Advance(SeenTracker.UndoFor);   // posts the expiry
        tracker.MarkAllSeen(["b", "c"]);       // a new offer, before the post runs
        await Task.Yield();

        Assert.Equal(2, tracker.UndoCount);
    }, Token);

    [Fact]
    public Task A_posted_expiry_does_not_end_an_offer_the_user_holds() => UiThread.Run(async () =>
    {
        var tracker = new SeenTracker(new Preferences(new InMemoryPreferencesStore()), _clock);
        tracker.MarkAllSeen(["a", "b"]);
        _clock.Advance(SeenTracker.UndoFor);
        tracker.HoldUndo(holding: true);
        await Task.Yield();

        Assert.Equal(2, tracker.UndoCount);
    }, Token);

    [Fact]
    public Task A_posted_dwell_does_not_mark_a_row_that_left_the_screen() => UiThread.Run(async () =>
    {
        var tracker = new SeenTracker(new Preferences(new InMemoryPreferencesStore()), _clock);
        tracker.Watch("a", onScreenAndFocused: true);
        _clock.Advance(SeenTracker.OnScreenFor);
        tracker.Watch("a", onScreenAndFocused: false);
        await Task.Yield();

        Assert.False(tracker.IsSeen("a"));
    }, Token);

    [Fact]
    public Task The_OS_motion_setting_changing_before_any_list_still_reaches_the_rows_on_the_UI_thread() => UiThread.Run(async () =>
    {
        var ui = Environment.CurrentManagedThreadId;
        var motion = new FakeMotionPreference();
        var seen = new NeedsYouSeenViewModel(new Preferences(new InMemoryPreferencesStore()), motion, _clock);
        var threads = new List<int>();
        seen.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

        await Task.Run(() => motion.Set(reduce: true), Token);   // before Update or Restore
        await Task.Yield();

        Assert.NotEmpty(threads);
        Assert.All(threads, t => Assert.Equal(ui, t));
    }, Token);
}
