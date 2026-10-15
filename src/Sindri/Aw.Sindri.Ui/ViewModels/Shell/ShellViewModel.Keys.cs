using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The shell's single keys (<see cref="ShellViewModel"/>, continued; UI spec §8): the G chord (1 s, "G …" in the top bar),
/// J / K between rows, Enter to open the selected row's sheet, and C to create an item. The window passes a key only when
/// no text field has focus and no layer is open.
/// </summary>
internal sealed partial class ShellViewModel
{
    // §8: "The chord waits 1 s"; on the injected clock so the unit lane tests it.
    internal static readonly TimeSpan ChordWait = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _clock;
    private DateTimeOffset? _chordAt;

    /// <summary>"G …" while the chord waits for its second key, else empty.</summary>
    public string ChordHint { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>One single key (a letter, "Enter"); true when it did something.</summary>
    /// <param name="key">The key: "G", "J", "K", "C", "Enter", …</param>
    /// <param name="inSheet">True when focus is in the detail sheet: the row keys (J, K, Enter, Y) belong to the sheet then.</param>
    /// <returns>True when the key was handled.</returns>
    internal bool Press(string key, bool inSheet = false)
    {
        // §8 key guard: for 500 ms after a refresh moved the selection on its own, single keys do nothing.
        if (Queue.IsGuarded)
        {
            return true;
        }

        if (_chordAt is { } at)
        {
            (_chordAt, ChordHint) = (null, string.Empty);
            return _clock.GetUtcNow() - at <= ChordWait && Go(key);
        }

        if (key == "G")
        {
            (_chordAt, ChordHint) = (_clock.GetUtcNow(), Labels.ChordHint);
            return true;
        }

        return Single(key, inSheet) || RowKey(key, inSheet);
    }

    // Y copies the selected row's CLI (§8); / opens the Queue page's filter.
    private bool RowKey(string key, bool inSheet)
    {
        if (key == "Y" && !inSheet && OnRowPage && Queue.HasSelectionOn(RowScope) && Queue.SelectedCopy() is { } control)
        {
            _ = control.Copy(CancellationToken.None);
            return true;
        }

        if (key != "/" || !IsQueue)
        {
            return false;
        }

        Queue.OpenFilter();
        return true;
    }

    // C, J / K and Enter (§8), scoped to the pages with rows and, for the row keys, to the page rather than the sheet.
    private bool Single(string key, bool inSheet)
    {
        switch (key)
        {
            case "C":
                Create.Open();
                return true;
            case "J" or "K" when !inSheet && OnRowPage:
                Queue.Move(key == "J" ? 1 : -1, RowScope);
                return true;
            case "Enter" when !inSheet && OnRowPage && Queue.HasSelectionOn(RowScope) && Queue.SelectedId is { } id:
                OpenItem(id);
                return true;
            default:
                return false;
        }
    }

    // The pages with rows, and the rows the keys walk there: Home's panels or the Queue page's groups.
    private bool OnRowPage => IsHome || IsQueue;

    private QueueScope RowScope => RowScopeOf(CurrentPage) ?? QueueScope.Page;

    // The rows' scope for a page the shell shows (the queue follows it, silently); null for a page that draws none.
    private static QueueScope? RowScopeOf(PageId page) => page == PageId.Home ? QueueScope.Home : page == PageId.Queue ? QueueScope.Page : null;

    // The chord's second key (§8): N H Q R W D S and '.'.
    private bool Go(string key)
    {
        switch (key)
        {
            case "N" or "H":
                GoTo(PageId.Home);
                return true;
            case "Q":
                GoToQueue(QueueFilter.All);
                return true;
            case "R":
                GoToQueue(QueueFilter.Running);
                return true;
            case "W":
                GoToQueue(QueueFilter.WithYou);
                return true;
            case "D":
                GoTo(PageId.Decisions);
                return true;
            case "S":
                GoTo(PageId.Services);
                return true;
            case ".":
                GoTo(PageId.Health);
                return true;
            default:
                return false;
        }
    }
}
