using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The app's real start-up path, the way the desktop head runs it: <see cref="SindriApp.Create"/> over the platform
/// ports, then the framework initialization with a classic desktop lifetime. The window shows with Settings in its
/// slot, the tray follows the shell, and the single instance's messages are consumed.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StartupTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeTray _tray = new();
    private readonly FakeInstanceChannel _channel = new();
    private readonly ClassicDesktopStyleApplicationLifetime _desktop = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _desktop.Dispose();

    private static async Task OnUi(Func<Task> body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StartupTests).Assembly)
            .Dispatch(async () =>
            {
                await body();
                return true;
            }, Token);

    private SindriPlatform Platform(FakeSingleInstance instance) =>
        new(new InMemoryEndpointRecords(), new InMemoryPreferencesStore(), new FakeMotionPreference(), instance, _tray);

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10, Token);
            Dispatcher.UIThread.RunJobs();
        }
    }

    // Quit from the tray (the lifetime's shutdown) and close what start-up opened, so nothing is left for the next test.
    private void Quit()
    {
        _tray.ClickQuit();
        foreach (var window in _desktop.Windows)
        {
            window.Close();
        }

        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Start_up_shows_the_shell_with_settings_and_starts_the_tray_and_the_single_instance() => OnUi(async () =>
    {
        var instance = new FakeSingleInstance(_channel);
        Assert.True(await instance.TryClaim(Token));
        var app = SindriApp.Create(Platform(instance), _clock);
        app.ApplicationLifetime = _desktop;
        app.Initialize();

        app.OnFrameworkInitializationCompleted();
        await Until(() => _desktop.MainWindow is { IsVisible: true } && _tray.Shown is not null);

        try
        {
            var window = Assert.IsType<ShellWindow>(_desktop.MainWindow);
            Assert.True(window.IsVisible, "the shell window never showed");
            _ = Assert.IsType<SettingsView>(window.SettingsSlot.Content);
            Assert.NotNull(_tray.Shown);

            // A later launch's deep link is consumed: the running app navigates on it.
            Assert.True(await new FakeSingleInstance(_channel).Send("item/ITM-01JC8", Token));
            var shell = Assert.IsType<ShellViewModel>(window.DataContext);
            await Until(() => shell.IsSheetOpen);
            Assert.Equal("ITM-01JC8", shell.SheetSubject);
        }
        finally
        {
            Quit();
            await instance.DisposeAsync();
        }
    });
}
