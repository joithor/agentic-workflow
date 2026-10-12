using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory tray: remembers what it shows and lets a test click its menu.</summary>
internal sealed class FakeTray : ITray
{
    private readonly List<TrayView> _history = [];
    private Exception? _fail;

    public event EventHandler? OpenRequested;

    public event EventHandler? QuitRequested;

    public TrayView? Shown { get; private set; }

    /// <summary>Every view shown, in order.</summary>
    public IReadOnlyList<TrayView> History => _history;

    public void Show(TrayView view)
    {
        if (Interlocked.Exchange(ref _fail, value: null) is { } error)
        {
            throw error;
        }

        Shown = view;
        _history.Add(view);
    }

    /// <summary>Makes the next show throw, as a tray the platform can't create does.</summary>
    /// <param name="error">The exception.</param>
    public void FailNextShow(Exception error) => _fail = error;

    /// <summary>Picks "Open Sindri".</summary>
    public void ClickOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Picks "Quit".</summary>
    public void ClickQuit() => QuitRequested?.Invoke(this, EventArgs.Empty);
}
