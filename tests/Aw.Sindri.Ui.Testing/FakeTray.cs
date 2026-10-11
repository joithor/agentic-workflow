using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory tray: remembers what it shows and lets a test click its menu.</summary>
internal sealed class FakeTray : ITray
{
    private readonly List<TrayView> _history = [];

    public event EventHandler? OpenRequested;

    public event EventHandler? QuitRequested;

    public TrayView? Shown { get; private set; }

    /// <summary>Every view shown, in order.</summary>
    public IReadOnlyList<TrayView> History => _history;

    public void Show(TrayView view)
    {
        Shown = view;
        _history.Add(view);
    }

    /// <summary>Picks "Open Sindri".</summary>
    public void ClickOpen() => OpenRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Picks "Quit".</summary>
    public void ClickQuit() => QuitRequested?.Invoke(this, EventArgs.Empty);
}
