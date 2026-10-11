using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The Sindri app: the theme, and on a desktop the main window following the control plane. A head creates it with
/// <see cref="Create(IEndpointRecordSource, TimeProvider)"/>, passing its platform adapters; the parameterless constructor (theme only) is for the XAML loader
/// and headless view tests.
/// </summary>
public sealed class SindriApp : Application
{
    private readonly MainViewModel? _main;

    /// <summary>The app with its theme only: no window, nothing followed.</summary>
    public SindriApp()
    {
    }

    private SindriApp(MainViewModel main)
    {
        _main = main;
    }

    /// <summary>The app following the control plane whose endpoint record <paramref name="records"/> reads.</summary>
    /// <param name="records">The platform's endpoint record (control.json on a desktop).</param>
    /// <param name="clock">The clock for retry waits.</param>
    /// <returns>The app.</returns>
    public static SindriApp Create(IEndpointRecordSource records, TimeProvider clock) =>
        new(new MainViewModel(records, new SindriApiClient(Loopback()), clock));

    /// <summary>
    /// The app in the U0 shell (UI spec §3.2, §15): the shell window over <paramref name="platform"/>'s adapters, closing
    /// to the tray, and the single instance's messages navigating it. CONTRACT (U0 Unit C): Unit A builds the shell view
    /// model here and Unit B wires the tray, the single instance and close-to-tray; the head switches to it in Unit B.
    /// </summary>
    /// <param name="platform">The head's adapters.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <returns>The app.</returns>
    public static SindriApp Create(SindriPlatform platform, TimeProvider clock) => throw new NotSupportedException();

    // The API is on loopback: never through a configured proxy (as the CLI's client), and a connect that hangs fails in 2 s.
    // The client owns no timeout of its own, so the event stream stays open.
    private static HttpClient Loopback() =>
        new(new SocketsHttpHandler { UseProxy = false, ConnectTimeout = TimeSpan.FromSeconds(2) }) { Timeout = Timeout.InfiniteTimeSpan };

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (_main is not null && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = _main };

            // Started on the UI thread, so the view model's awaits come back to it; the process ends with the window.
            _ = _main.Run(CancellationToken.None);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
