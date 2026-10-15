using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Close-to-tray, the tray's menu, and a later launch's messages, on a real window under the headless platform.</summary>
[Lane(TestLane.Integration)]
public sealed class AppLifetimeTests
{
    private readonly FakeTray _tray = new();
    private readonly FakeInstanceChannel _channel = new();
    private readonly List<DeepLink> _navigated = [];
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private Func<Task> _settle = () => Task.CompletedTask;
    private int _settles;
    private int _shutdowns;

    private static Task OnUi(Action body) => OnUi(() =>
    {
        body();
        return Task.CompletedTask;
    });

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(AppLifetimeTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, TestContext.Current.CancellationToken);

    private (Window Window, AppLifetime Lifetime, FakeSingleInstance Instance) Start()
    {
        var window = new Window();
        window.Show();
        var instance = new FakeSingleInstance(_channel);
        return (window, new AppLifetime(window, _tray, _navigated.Add, () => _shutdowns++, Settle, _clock), instance);
    }

    private Task Settle()
    {
        _settles++;
        return _settle();
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public Task Closing_the_window_hides_it_and_keeps_the_app() => OnUi(() =>
    {
        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Close();

            Assert.False(window.IsVisible);
            Assert.Equal(0, _shutdowns);
        }
    });

    [Fact]
    public Task Open_from_the_tray_shows_the_window_and_quit_ends_the_app() => OnUi(() =>
    {
        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Close();
            _tray.ClickOpen();
            Assert.True(window.IsVisible);

            _tray.ClickQuit();
            window.Close();

            Assert.Equal(1, _shutdowns);
            Assert.False(window.IsVisible);
        }
    });

    // Quit records the window's bounds and waits for the preferences to be written; only then does the app end.
    [Fact]
    public Task Quit_ends_the_app_once_the_preferences_are_written() => OnUi(async () =>
    {
        var written = new TaskCompletionSource();
        _settle = () => written.Task;
        var (_, lifetime, _) = Start();
        using (lifetime)
        {
            _tray.ClickQuit();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal((1, 0), (_settles, _shutdowns));

            written.SetResult();
            await Until(() => _shutdowns > 0);

            Assert.Equal(1, _shutdowns);
        }
    });

    [Fact]
    public Task Quit_ends_the_app_after_a_second_when_the_write_hangs() => OnUi(async () =>
    {
        _settle = () => new TaskCompletionSource().Task;
        var (_, lifetime, _) = Start();
        using (lifetime)
        {
            _tray.ClickQuit();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, _shutdowns);

            _clock.Advance(AppLifetime.SettleWithin);
            await Until(() => _shutdowns > 0);

            Assert.Equal(1, _shutdowns);
        }
    });

    // ⌘Q or logging out: not cancelled (that would hold up a logout), but the bounds are recorded and written first.
    [Fact]
    public Task The_OS_ending_the_app_records_the_bounds_and_lets_it_end() => OnUi(() =>
    {
        var (_, lifetime, _) = Start();
        using (lifetime)
        {
            var request = new ShutdownRequestedEventArgs();

            lifetime.OnShutdownRequested(sender: null, request);

            Assert.Equal(1, _settles);
            Assert.False(request.Cancel);
        }
    });

    // Review run 5 (julik-frontend-races-reviewer-0): the single instance must stop taking messages the moment ending
    // begins, not when the process is finally gone, so Quit says so before it waits for the preferences.
    [Fact]
    public Task Quit_says_it_is_quitting_before_it_waits_for_the_preferences() => OnUi(() =>
    {
        var written = new TaskCompletionSource();
        _settle = () => written.Task;
        var (_, lifetime, _) = Start();
        using (lifetime)
        {
            var quitting = 0;
            lifetime.Quitting += (_, _) => quitting++;

            _tray.ClickQuit();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal((1, 0), (quitting, _shutdowns));
            written.SetResult();
        }
    });

    [Fact]
    public Task The_OS_ending_the_app_says_it_is_quitting_before_the_bounds_are_written() => OnUi(() =>
    {
        var (_, lifetime, _) = Start();
        using (lifetime)
        {
            var quitting = 0;
            var settlesWhenSaid = -1;
            lifetime.Quitting += (_, _) =>
            {
                quitting++;
                settlesWhenSaid = _settles;
            };

            lifetime.OnShutdownRequested(sender: null, new ShutdownRequestedEventArgs());

            Assert.Equal((1, 0), (quitting, settlesWhenSaid));
        }
    });

    // A message that arrives once quitting has begun is not acted on.
    [Fact]
    public Task A_message_received_once_quitting_has_begun_is_not_acted_on() => OnUi(() =>
    {
        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Close();
            lifetime.OnShutdownRequested(sender: null, new ShutdownRequestedEventArgs());

            lifetime.Receive("item/ITM-01JC8");

            Assert.False(window.IsVisible);
            Assert.Empty(_navigated);
        }
    });

    // The real wiring: the shell window's bounds reach the store before the app ends.
    [Fact]
    public Task Quit_writes_the_shell_windows_bounds_before_the_app_ends() => OnUi(async () =>
    {
        var store = new GatedPreferencesStore();
        var preferences = new Preferences(store);
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), _clock, preferences, new FakeClipboard());
        await shell.Restore(TestContext.Current.CancellationToken);
        var window = new ShellWindow { DataContext = shell, Width = 1000, Height = 700 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        _settle = () =>
        {
            window.RememberBounds();
            return preferences.Flush();
        };
        using var lifetime = new AppLifetime(window, _tray, _navigated.Add, () => _shutdowns++, Settle, _clock);
        store.HoldWrites();

        _tray.ClickQuit();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, _shutdowns);
        store.ReleaseWrites();
        await Until(() => _shutdowns > 0);

        Assert.Equal(1, _shutdowns);
        Assert.Equal((1000d, 700d), (await preferences.Load(TestContext.Current.CancellationToken)).Window is { } w ? (w.Width, w.Height) : (0d, 0d));
        window.Close();
    });

    [Fact]
    public Task Focus_brings_the_window_back_and_navigates_nowhere() => OnUi(() =>
    {
        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Close();

            lifetime.Receive(InstanceMessages.Focus);

            Assert.True(window.IsVisible);
            Assert.Empty(_navigated);
        }
    });

    [Fact]
    public Task A_deep_link_brings_the_window_back_and_navigates() => OnUi(() =>
    {
        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Close();

            lifetime.Receive("item/ITM-01JC8");

            Assert.True(window.IsVisible);
            Assert.Equal([new DeepLink(DeepLinkKind.Item, "ITM-01JC8")], _navigated);
        }
    });

    // A message never acts: words that look like commands, or links the app doesn't know, change nothing.
    [Theory]
    [InlineData("quit")]
    [InlineData("shutdown now")]
    [InlineData("item/../../etc")]
    [InlineData("")]
    public Task Anything_else_is_ignored(string message) => OnUi(() =>
    {
        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Close();

            lifetime.Receive(message);

            Assert.False(window.IsVisible);
            Assert.Empty(_navigated);
            Assert.Equal(0, _shutdowns);
        }
    });

    [Fact]
    public Task A_later_launchs_message_arrives_through_the_instance() => OnUi(async () =>
    {
        var (window, lifetime, instance) = Start();
        using (lifetime)
        {
            Assert.True(await instance.TryClaim(TestContext.Current.CancellationToken));
            var listener = new InstanceListener(instance);
            using var stop = new CancellationTokenSource();
            var listening = listener.Listen(stop.Token);
            listener.Deliver(lifetime.Receive);
            window.Close();

            Assert.True(await new FakeSingleInstance(_channel).Send("service/heartbeat", TestContext.Current.CancellationToken));
            for (var i = 0; i < 200 && _navigated.Count == 0; i++)
            {
                await Task.Delay(5, TestContext.Current.CancellationToken);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal([new DeepLink(DeepLinkKind.Service, "heartbeat")], _navigated);
            Assert.True(window.IsVisible);
            await stop.CancelAsync();
            await listening;
        }
    });

    // A later launch while this one is still starting (no window yet) is answered at once: "focus" needs nothing, since
    // the window is about to show, and the latest deep link waits for the window, which then goes there.
    [Fact]
    public Task A_message_before_the_window_shows_is_answered_and_its_link_applied_once_it_shows() => OnUi(async () =>
    {
        var instance = new FakeSingleInstance(_channel);
        Assert.True(await instance.TryClaim(TestContext.Current.CancellationToken));
        var listener = new InstanceListener(instance);
        using var stop = new CancellationTokenSource();
        var listening = listener.Listen(stop.Token);
        var later = new FakeSingleInstance(_channel);

        Assert.True(await later.Send(InstanceMessages.Focus, TestContext.Current.CancellationToken));
        Assert.True(await later.Send("item/ITM-OLD", TestContext.Current.CancellationToken));
        Assert.True(await later.Send("service/heartbeat", TestContext.Current.CancellationToken));
        Assert.Empty(_navigated);

        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Hide();
            listener.Deliver(lifetime.Receive);

            Assert.Equal([new DeepLink(DeepLinkKind.Service, "heartbeat")], _navigated);
            Assert.True(window.IsVisible);
            await stop.CancelAsync();
            await listening;
        }
    });

    // Review run 3 (julik-frontend-races-reviewer-2): the listener took a message and queued it for the UI thread, which
    // was busy; meanwhile the sender's wait ended and the message was withdrawn. When the UI thread gets to it, its claim
    // fails and it drops the message: the window doesn't come forward, nothing navigates, and the sender heard no. Driven
    // over the shared fake, whose withdrawal the single-instance contract pins (review run 4,
    // pattern-recognition-specialist-0).
    [Fact]
    public Task A_message_withdrawn_before_the_UI_thread_gets_to_it_is_not_acted_on() => OnUi(async () =>
    {
        var channel = new FakeInstanceChannel(_clock);
        var (window, lifetime, _) = Start();
        using (lifetime)
        {
            window.Hide();
            await using var instance = new FakeSingleInstance(channel);
            Assert.True(await instance.TryClaim(TestContext.Current.CancellationToken));
            var listener = new InstanceListener(instance);
            listener.Deliver(lifetime.Receive);
            using var stop = new CancellationTokenSource();
            var listening = Task.Run(() => listener.Listen(stop.Token), TestContext.Current.CancellationToken);
            Assert.True(SpinWait.SpinUntil(() => channel.Reading, TimeSpan.FromSeconds(5)));
            var sending = Task.Run(() => new FakeSingleInstance(channel, processId: 5150).Send("service/heartbeat", TestContext.Current.CancellationToken).AsTask(), TestContext.Current.CancellationToken);

            // The UI thread is busy (blocked here) while the listener takes the message and queues it; then the sender's
            // wait ends on the clock.
            Assert.True(SpinWait.SpinUntil(() => channel.Taken == 1, TimeSpan.FromSeconds(5)));
            _ = SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(200)); // still blocking: the listener queues it meanwhile
            _clock.Advance(FakeInstanceChannel.HandleWithin);
            Assert.True(SpinWait.SpinUntil(() => sending.IsCompleted, TimeSpan.FromSeconds(5))); // the withdrawal runs before the UI thread is free
            Assert.False(await sending);
            await Until(() => false);
            await stop.CancelAsync();
            await listening;

            Assert.Empty(_navigated);
            Assert.False(window.IsVisible);
        }
    });

    [Fact]
    public Task The_tray_shows_the_shell_state() => OnUi(() =>
    {
        var (_, lifetime, _) = Start();
        using (lifetime)
        {
            lifetime.ShowState(ShellState.NotRunning);

            Assert.Equal(AppLifetime.TrayViewFor(ShellState.NotRunning), _tray.Shown);
        }
    });
}
