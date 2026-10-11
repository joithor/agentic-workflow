using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The Sindri app: the theme, and on a desktop the shell window following the control plane (UI spec §3.2, §15). A head
/// creates it with <see cref="Create(SindriPlatform, TimeProvider)"/>, passing its platform adapters; the parameterless
/// constructor (theme only) is for the XAML loader and headless view tests. The platform wiring (tray, single instance,
/// close-to-tray) is another part of this class, which receives the window through ShellOpened.
/// </summary>
public sealed partial class SindriApp : Application
{
    private readonly ShellViewModel? _shell;

    /// <summary>The app with its theme only: no window, nothing followed.</summary>
    public SindriApp()
    {
    }

    private SindriApp(ShellViewModel shell, SindriPlatform platform)
    {
        _shell = shell;
        Platform = platform;
    }

    /// <summary>The app in the shell (UI spec §3.2, §15): the shell window over <paramref name="platform"/>'s adapters.</summary>
    /// <param name="platform">The head's adapters.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <returns>The app.</returns>
    public static SindriApp Create(SindriPlatform platform, TimeProvider clock) =>
        new(new ShellViewModel(platform.Records, new SindriApiClient(Loopback()), clock, platform.Preferences), platform);

    /// <summary>The head's adapters, or null for the theme-only app.</summary>
    internal SindriPlatform? Platform { get; }

    // The API is on loopback: never through a configured proxy (as the CLI's client), and a connect that hangs fails in 2 s.
    // The client owns no timeout of its own, so the event stream stays open.
    private static HttpClient Loopback() =>
        new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(2) }) { Timeout = Timeout.InfiniteTimeSpan };

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (_shell is not null && Platform is { } platform && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Started on the UI thread, so the view models' awaits come back to it.
            _ = Open(desktop, _shell, platform);
        }

        base.OnFrameworkInitializationCompleted();
    }

    // The window opens once the preferences are read (the text scale and reduced motion, applied before the first frame;
    // its bounds, the sheet width and the last page), then follows.
    private async Task Open(IClassicDesktopStyleApplicationLifetime desktop, ShellViewModel shell, SindriPlatform platform)
    {
        var settings = await Settings(platform).ConfigureAwait(true);
        desktop.Exit += (_, _) => settings.Dispose();
        await shell.Restore(CancellationToken.None).ConfigureAwait(true);
        var window = new ShellWindow { DataContext = shell };
        window.SettingsSlot.Content = new SettingsView { DataContext = settings };
        desktop.MainWindow = window;
        window.Show();
        ShellOpened(desktop, window, shell);
        await shell.Run(CancellationToken.None).ConfigureAwait(true);
    }

    // Settings (⌘,) over the preferences and the OS setting, applying to this app's resources. A preferences file that
    // can't be read leaves the theme's own values (the defaults) in place.
    private async Task<SettingsViewModel> Settings(SindriPlatform platform)
    {
        var settings = new SettingsViewModel(new Preferences(platform.Preferences), platform.Motion, SettingsTheme.For(Resources));
        try
        {
            await settings.Load(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The defaults stay; a later change in Settings tries the file again.
        }

        return settings;
    }

    /// <summary>The platform wiring's hook: the shell window is shown and its view model about to follow.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    /// <param name="window">The shell window.</param>
    /// <param name="shell">Its view model (a deep link from the single instance navigates it).</param>
    partial void ShellOpened(IClassicDesktopStyleApplicationLifetime desktop, ShellWindow window, ShellViewModel shell);
}
