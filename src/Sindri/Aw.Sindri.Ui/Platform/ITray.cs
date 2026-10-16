namespace Aw.Sindri.Ui.Platform;

/// <summary>
/// Port: the tray or menu-bar icon (UI spec §5 "Tray / menu-bar badge"). The app keeps it while it runs; closing the window
/// keeps the app in the tray, and Quit is explicit. The app decides what it shows (<see cref="TrayView"/>, words from its
/// labels); the head draws it (Avalonia's TrayIcon on a desktop) and reports the menu's clicks. No action runs from the
/// tray. Called on the UI thread.
/// </summary>
public interface ITray
{
    /// <summary>What the tray shows now, or null before the first <see cref="Show"/>.</summary>
    public TrayView? Shown { get; }

    /// <summary>Raised when the user picks "Open Sindri" (or clicks the icon where the platform has no menu on click).</summary>
    public event EventHandler? OpenRequested;

    /// <summary>Raised when the user picks a needs-you row in the menu; the app opens it. No action runs from the tray.</summary>
    public event EventHandler<TrayRowEventArgs>? RowRequested;

    /// <summary>Raised when the user picks "Quit".</summary>
    public event EventHandler? QuitRequested;

    /// <summary>Shows a state, its tooltip and the menu's labels.</summary>
    /// <param name="view">What to show.</param>
    public void Show(TrayView view);
}
