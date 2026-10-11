using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>Close-to-tray, the tray's menu, and a later launch's messages, on a real window under the headless platform.</summary>
[Lane(TestLane.Integration)]
public sealed class AppLifetimeTests
{
    private readonly FakeTray _tray = new();
    private readonly FakeInstanceChannel _channel = new();
    private readonly List<DeepLink> _navigated = [];
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
        return (window, new AppLifetime(window, _tray, instance, _navigated.Add, () => _shutdowns++), instance);
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
            using var stop = new CancellationTokenSource();
            var listening = lifetime.Listen(stop.Token);
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
