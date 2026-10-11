using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The desktop app's life around its window (UI spec §5, §15): closing the window keeps the app in the tray and Quit
/// ends it; the tray shows the shell's state; a later launch's message brings the window forward, and a deep link also
/// navigates. A message never runs an action: anything that isn't "focus" or a deep link is ignored.
/// </summary>
internal sealed class AppLifetime : IDisposable
{
    private readonly Window _window;
    private readonly ITray _tray;
    private readonly ISingleInstance _instance;
    private readonly Action<DeepLink> _navigate;
    private readonly Action _shutdown;
    private bool _quitting;

    /// <summary>Wires the window, the tray and the instance's messages.</summary>
    /// <param name="window">The main window.</param>
    /// <param name="tray">The tray icon.</param>
    /// <param name="instance">The claimed instance whose messages arrive here.</param>
    /// <param name="navigate">Goes where a deep link points (the shell's Navigate).</param>
    /// <param name="shutdown">Ends the app (the lifetime's Shutdown).</param>
    internal AppLifetime(Window window, ITray tray, ISingleInstance instance, Action<DeepLink> navigate, Action shutdown)
    {
        (_window, _tray, _instance, _navigate, _shutdown) = (window, tray, instance, navigate, shutdown);
        _window.Closing += OnClosing;
        _tray.OpenRequested += OnOpen;
        _tray.QuitRequested += OnQuit;
    }

    /// <summary>
    /// Starts the app's life on a desktop: the app ends only on Quit, the tray follows the shell's state, and the
    /// instance's messages arrive until the app ends.
    /// </summary>
    /// <param name="desktop">The desktop lifetime.</param>
    /// <param name="window">The main window.</param>
    /// <param name="platform">The head's adapters.</param>
    /// <param name="shell">The shell the window shows.</param>
    /// <returns>The lifetime (it lives as long as the app).</returns>
    internal static AppLifetime Start(IClassicDesktopStyleApplicationLifetime desktop, Window window, SindriPlatform platform, ShellViewModel shell)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var lifetime = new AppLifetime(window, platform.Tray, platform.Instance, shell.Navigate, () => desktop.Shutdown());
        lifetime.ShowState(shell.State);
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.State))
            {
                lifetime.ShowState(shell.State);
            }
        };
        _ = lifetime.Listen(CancellationToken.None);
        return lifetime;
    }

    /// <summary>What the tray shows for a shell state: "running" only once connected and fresh; hollow until then.</summary>
    /// <param name="state">The shell's state.</param>
    /// <returns>The tray's view.</returns>
    internal static TrayView TrayViewFor(ShellState state) => state switch
    {
        ShellState.Normal => new TrayView(TrayState.Neutral, Labels.TrayRunning, Labels.TrayOpen, Labels.TrayQuit),
        ShellState.NotRunning => new TrayView(TrayState.NotRunning, Labels.TrayNotRunning, Labels.TrayOpen, Labels.TrayQuit),
        ShellState.VersionMismatch or ShellState.Connecting or ShellState.Unreadable or ShellState.Stale => Hollow,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, message: null),
    };

    private static TrayView Hollow { get; } = new(TrayState.Stale, Labels.TrayNotResponding, Labels.TrayOpen, Labels.TrayQuit);

    /// <summary>Shows the shell's state in the tray.</summary>
    /// <param name="state">The state.</param>
    internal void ShowState(ShellState state) => _tray.Show(TrayViewFor(state));

    /// <summary>Handles one message from a later launch (on the UI thread): focus, or a deep link; nothing else.</summary>
    /// <param name="message">The message's text, unparsed.</param>
    internal void Receive(string message)
    {
        if (string.Equals(message, InstanceMessages.Focus, StringComparison.Ordinal))
        {
            BringForward();
        }
        else if (DeepLink.TryParse(message, out var link))
        {
            BringForward();
            _navigate(link);
        }
    }

    /// <summary>Reads the instance's messages until cancelled, handling each on the UI thread.</summary>
    /// <param name="cancellationToken">Stops listening.</param>
    /// <returns>A task that ends when cancelled or the instance ends.</returns>
    internal async Task Listen(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _instance.Messages(cancellationToken).ConfigureAwait(false))
            {
                Dispatcher.UIThread.Post(() => Receive(message));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopped.
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _window.Closing -= OnClosing;
        _tray.OpenRequested -= OnOpen;
        _tray.QuitRequested -= OnQuit;
    }

    private void BringForward()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    // Close-to-tray: the user's close hides the window; Quit, and the OS or app shutting down, close it.
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_quitting || e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            return;
        }

        e.Cancel = true;
        _window.Hide();
    }

    private void OnOpen(object? sender, EventArgs e) => BringForward();

    private void OnQuit(object? sender, EventArgs e)
    {
        _quitting = true;
        _shutdown();
    }
}
