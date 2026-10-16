using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
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
    private readonly Action<DeepLink> _navigate;
    private readonly Action _shutdown;
    private readonly Func<Task> _settle;
    private readonly TimeProvider _clock;
    private readonly IDockBadge _badge;
    /// <summary>Raised on the UI thread the moment Quit, or the OS ending the app, begins (the single instance retires on it).</summary>
    internal event EventHandler? Quitting
    {
        add => _onQuitting += value;
        remove => _onQuitting -= value;
    }

    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private EventHandler? _onQuitting;
    private bool _quitting;

    /// <summary>Wires the window and the tray.</summary>
    /// <param name="window">The main window.</param>
    /// <param name="tray">The tray icon.</param>
    /// <param name="navigate">Goes where a deep link points (the shell's Navigate).</param>
    /// <param name="shutdown">Ends the app (the lifetime's Shutdown).</param>
    /// <param name="settle">Records the window's bounds and returns the preferences writes still pending; the app ends once they are done, or after <see cref="SettleWithin"/>.</param>
    /// <param name="clock">The clock <see cref="SettleWithin"/> runs on.</param>
    /// <param name="badge">The dock or taskbar badge; none when null.</param>
    internal AppLifetime(Window window, ITray tray, Action<DeepLink> navigate, Action shutdown, Func<Task> settle, TimeProvider clock, IDockBadge? badge = null)
    {
        _badge = badge ?? new NullDockBadge();
        (_window, _tray, _navigate, _shutdown, _settle, _clock) = (window, tray, navigate, shutdown, settle, clock);
        _window.Closing += OnClosing;
        _tray.OpenRequested += OnOpen;
        _tray.QuitRequested += OnQuit;
        _tray.RowRequested += OnRow;
    }

    /// <summary>
    /// Starts the app's life on a desktop: the app ends only on Quit or when the OS ends it (each first writing the
    /// preferences), and the tray follows the shell's state.
    /// </summary>
    /// <param name="desktop">The desktop lifetime.</param>
    /// <param name="shell">The shell the window shows.</param>
    internal void Start(IClassicDesktopStyleApplicationLifetime desktop, ShellViewModel shell)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _desktop = desktop;
        desktop.ShutdownRequested += OnShutdownRequested;
        Follow(shell);
    }

    /// <summary>Shows what the needs-you rows add up to in the tray and on the dock or taskbar badge.</summary>
    /// <param name="summary">The summary.</param>
    internal void ShowAttention(AttentionSummary summary)
    {
        _tray.Show(summary.Tray);
        _badge.Show(summary.Badge);
    }

    /// <summary>Follows the shell's attention summary from now on, whether or not the window is shown.</summary>
    /// <param name="shell">The shell.</param>
    internal void Follow(ShellViewModel shell)
    {
        ShowAttention(shell.Attention);
        shell.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ShellViewModel.Attention), StringComparison.Ordinal))
            {
                ShowAttention(shell.Attention);
            }
        };
    }

    /// <summary>Handles one message from a later launch (on the UI thread; <see cref="InstanceListener"/> hands them over): focus, or a deep link; nothing else.</summary>
    /// <param name="message">The message's text, unparsed.</param>
    internal void Receive(string message)
    {
        if (_quitting)
        {
            return;   // nothing is shown for a message once the app is ending
        }

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

    /// <inheritdoc />
    public void Dispose()
    {
        _desktop?.ShutdownRequested -= OnShutdownRequested;
        _window.Closing -= OnClosing;
        _tray.OpenRequested -= OnOpen;
        _tray.QuitRequested -= OnQuit;
        _tray.RowRequested -= OnRow;
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

    // A row picked in the tray's menu opens the app at needs-you/<rowId>: it navigates and runs nothing (UI spec §5).
    private void OnRow(object? sender, TrayRowEventArgs e)
    {
        if (_quitting)
        {
            return;
        }

        BringForward();
        _navigate(new DeepLink(DeepLinkKind.NeedsYou, e.RowId));
    }

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
        _onQuitting?.Invoke(this, EventArgs.Empty);
        try
        {
            await _settle().WaitAsync(SettleWithin, _clock).ConfigureAwait(true);
        }
        catch (TimeoutException)
        {
            // A write still pending ends with the app: the preferences are a convenience.
        }

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
        _quitting = true;
        _onQuitting?.Invoke(this, EventArgs.Empty);
        try
        {
#pragma warning disable RS0030 // Avalonia's shutdown-requested handler is synchronous; the wait is bounded by SettleWithin
            _settle().WaitAsync(SettleWithin, _clock).GetAwaiter().GetResult();
#pragma warning restore RS0030
        }
        catch (TimeoutException)
        {
            // As Quit: the app ends anyway.
        }
    }
}
