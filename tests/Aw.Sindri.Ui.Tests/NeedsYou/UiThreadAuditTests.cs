using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>B7 audit (coordinator): anything that raises PropertyChanged runs on the UI thread, whichever thread the cause came from.</summary>
[Lane(TestLane.Unit)]
public sealed class UiThreadAuditTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A store whose read completes on a pool thread, as a file read does.
    private sealed class PoolStore : IPreferencesStore
    {
        public string Location => "pool:preferences.json";

        public async ValueTask<byte[]?> Read(CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return null;
        }

        public ValueTask Write(byte[] contents, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    [Fact]
    public Task Restoring_the_seen_ids_raises_its_change_on_the_UI_thread() => UiThread.Run(async () =>
    {
        var ui = Environment.CurrentManagedThreadId;
        var tracker = new SeenTracker(new Preferences(new PoolStore()), new OffThreadTimers(A.Noon));
        var threads = new List<int>();
        tracker.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

        await tracker.Restore(Token);

        Assert.NotEmpty(threads);
        Assert.All(threads, t => Assert.Equal(ui, t));
    }, Token);

    [Fact]
    public Task The_OS_reduce_motion_setting_changing_on_the_pollers_thread_reaches_the_rows_on_the_UI_thread() => UiThread.Run(async () =>
    {
        var ui = Environment.CurrentManagedThreadId;
        var motion = new FakeMotionPreference();
        var seen = new NeedsYouSeenViewModel(new Preferences(new InMemoryPreferencesStore()), motion, new OffThreadTimers(A.Noon));
        seen.Update([NeedsYouRows.Park()]);
        var threads = new List<int>();
        seen.PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);
        seen.Rows[0].PropertyChanged += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

        await Task.Run(() => motion.Set(reduce: true), Token);   // the poller's timer thread
        await Task.Yield();

        Assert.NotEmpty(threads);
        Assert.All(threads, t => Assert.Equal(ui, t));
        Assert.True(seen.ReducedMotion);
    }, Token);
}
