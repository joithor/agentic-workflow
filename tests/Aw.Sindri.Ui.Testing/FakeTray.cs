using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory tray: remembers what it shows and lets a test click its menu.</summary>
internal sealed class FakeTray : ITray
{
    private readonly List<TrayView> _history = [];
    private Exception? _fail;

    public event EventHandler? OpenRequested;

    public event EventHandler? QuitRequested;

    public event EventHandler<TrayRowEventArgs>? RowRequested;

    public TrayView? Shown { get; private set; }

    /// <summary>The menu's labels as a platform renders them: the rows, Open, Running, Quit; each '_' doubled.</summary>
    public IReadOnlyList<string> MenuLabels => Shown is not { } view
        ? []
        : [.. view.MenuRows.Select(r => Escape(r.Label)), Escape(view.OpenLabel), .. view.RunningLabel.Length == 0 ? [] : (string[])[Escape(view.RunningLabel)], Escape(view.QuitLabel)];

    /// <summary>Picks the row at <paramref name="index"/> of the menu (a place among its rows).</summary>
    /// <param name="index">The row's place.</param>
    public void PickRow(int index) => RowRequested?.Invoke(this, new TrayRowEventArgs(Shown!.MenuRows.ElementAt(index).Id));

    private static string Escape(string label) => label.Replace("_", "__", StringComparison.Ordinal);

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
