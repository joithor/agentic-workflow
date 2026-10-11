using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The Sindri app: the theme, and on a desktop the main window following the control plane. A head creates it with
/// <see cref="Create"/>, passing its platform adapters; the parameterless constructor (theme only) is for the XAML loader
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
        new(new MainViewModel(records, new SindriApiClient(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }), clock));

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
