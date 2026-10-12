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
    /// <summary>How long Quit, or the OS ending the app, waits for the preferences to be written before the app ends.</summary>
    internal static readonly TimeSpan SettleWithin = TimeSpan.FromSeconds(1);

    private readonly Window _window;
    private readonly ITray _tray;
    private readonly ISingleInstance _instance;
    private readonly Action<DeepLink> _navigate;
    private readonly Action _shutdown;
    private readonly Func<Task> _settle;
    private readonly TimeProvider _clock;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private bool _quitting;

    /// <summary>Wires the window, the tray and the instance's messages.</summary>
    /// <param name="window">The main window.</param>
    /// <param name="tray">The tray icon.</param>
    /// <param name="instance">The claimed instance whose messages arrive here.</param>
    /// <param name="navigate">Goes where a deep link points (the shell's Navigate).</param>
    /// <param name="shutdown">Ends the app (the lifetime's Shutdown).</param>
    /// <param name="settle">Records the window's bounds and returns the preferences writes still pending; the app ends once they are done, or after <see cref="SettleWithin"/>.</param>
    /// <param name="clock">The clock <see cref="SettleWithin"/> runs on.</param>
    internal AppLifetime(Window window, ITray tray, ISingleInstance instance, Action<DeepLink> navigate, Action shutdown, Func<Task> settle, TimeProvider clock)
    {
        (_window, _tray, _instance, _navigate, _shutdown, _settle, _clock) = (window, tray, instance, navigate, shutdown, settle, clock);
        _window.Closing += OnClosing;
        _tray.OpenRequested += OnOpen;
        _tray.QuitRequested += OnQuit;
    }

    /// <summary>
    /// Starts the app's life on a desktop: the app ends only on Quit or when the OS ends it (each first writing the
    /// preferences), the tray follows the shell's state, and the instance's messages arrive until the app ends.
    /// </summary>
    /// <param name="desktop">The desktop lifetime.</param>
    /// <param name="shell">The shell the window shows.</param>
    internal void Start(IClassicDesktopStyleApplicationLifetime desktop, ShellViewModel shell)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _desktop = desktop;
        desktop.ShutdownRequested += OnShutdownRequested;
        ShowState(shell.State);
        shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.State))
            {
                ShowState(shell.State);
            }
        };
        _ = Listen(CancellationToken.None);
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

    /// <summary>Reads the instance's messages until cancelled, handling each on the UI thread before reading the next.</summary>
    /// <param name="cancellationToken">Stops listening.</param>
    /// <returns>A task that ends when cancelled or the instance ends.</returns>
    internal async Task Listen(CancellationToken cancellationToken)
    {
        try
        {
            // Handled on the UI thread before the next is read, so the sender's answer means the app acted on it.
            await foreach (var message in _instance.Messages(cancellationToken).ConfigureAwait(false))
            {
                await Dispatcher.UIThread.InvokeAsync(() => Receive(message)).GetTask().ConfigureAwait(false);
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
        _desktop?.ShutdownRequested -= OnShutdownRequested;
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

    private void OnQuit(object? sender, EventArgs e) => _ = Quit();

    /// <summary>Quit (the tray's): records the window's bounds and waits for the preferences to be written, then ends the app.</summary>
    /// <returns>A task that completes once the app is told to end.</returns>
    internal async Task Quit()
    {
        if (_quitting)
        {
            return;
        }

        _quitting = true;
        _ = _settle().WaitAsync(SettleWithin, _clock); // red: not awaited yet
        await Task.CompletedTask.ConfigureAwait(true);
        _shutdown();
    }

    /// <summary>
    /// The OS ends the app (⌘Q, the Dock's Quit, logging out). Not cancelled, since that would hold up a logout: the
    /// window's bounds are recorded while it is still open, and the UI thread waits up to <see cref="SettleWithin"/> for
    /// the write, which runs off it.
    /// </summary>
    /// <param name="sender">The lifetime.</param>
    /// <param name="e">The request.</param>
    internal void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // red: nothing recorded yet
    }
}
