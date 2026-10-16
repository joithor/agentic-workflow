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
}
