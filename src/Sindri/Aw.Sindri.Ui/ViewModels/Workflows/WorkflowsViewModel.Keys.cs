using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page's keys (<see cref="WorkflowsViewModel"/>, continued; U8 T7, UI spec §8): J / K walk the drawn rows across the groups, and
/// Enter, A and X act on the selected row. The selection is an address: a re-read keeps it on the same instance wherever the row moved,
/// and clears it when the row is gone, so no key ever acts on a row the human is not on.
/// </summary>
internal sealed partial class WorkflowsViewModel
{
    /// <summary>The address of the row the keys act on; null for none.</summary>
    public string? SelectedAddress { get; private set => Set(ref field, value); }

    /// <summary>The row the keys act on; null for none, and for one in a group the human collapsed.</summary>
    public InstanceRowViewModel? SelectedRow => Walk.FirstOrDefault(r => string.Equals(r.Address, SelectedAddress, StringComparison.Ordinal));

    /// <summary>The words a key said when it only names a later phase; empty otherwise.</summary>
    public string KeyNote { get; private set { Set(ref field, value); OnChanged(nameof(HasKeyNote)); } } = string.Empty;

    /// <summary>True when a key left a note.</summary>
    public bool HasKeyNote => KeyNote.Length > 0;

    // Every row the page holds, and the rows J / K walk: those drawn (a collapsed group's are not).
    private IEnumerable<InstanceRowViewModel> Shown => Groups.SelectMany(g => g.Rows).Concat(DoneRows);

    private List<InstanceRowViewModel> Walk => [.. Groups.Where(g => g.IsExpanded).SelectMany(g => g.Rows), .. IsDoneExpanded ? DoneRows : []];

    /// <summary>Selects a row by its address.</summary>
    /// <param name="address">The instance's address.</param>
    /// <returns>True when a row of that address is shown.</returns>
    internal bool Select(string address)
    {
        if (!Shown.Any(r => string.Equals(r.Address, address, StringComparison.Ordinal)))
        {
            return false;
        }

        KeyNote = string.Empty;
        SelectedAddress = address;
        Mark();
        return true;
    }

    /// <summary>Clears the selection.</summary>
    internal void Deselect()
    {
        SelectedAddress = null;
        Mark();
    }

    /// <summary>One single key on the page.</summary>
    /// <param name="key">J, K, Enter, A, X, B or N.</param>
    /// <returns>True when the key was used.</returns>
    internal bool Press(string key)
    {
        KeyNote = string.Empty;
        switch (key)
        {
            case "J" or "K":
                return Move(key == "J" ? 1 : -1);
            case "N":
                KeyNote = Labels.StartArrives;
                return true;
            default:
                return SelectedRow is { } row && PressOn(row, key);
        }
    }

    // Enter opens the row; A answers it (a protected answer is the lock's popover, a desk task its pending output); X asks to cancel it; B names its phase.
    private bool PressOn(InstanceRowViewModel row, string key)
    {
        switch (key)
        {
            case "Enter":
                OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link));
                return true;
            case "A" when row.Protected is { } locked:
                locked.SetOpen(open: true);
                return true;
            case "A" when row.CanAnswerAtDesk:
                OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link, FrameIntent.PendingOutput));
                return true;
            case "X" when row.CanCancel:
                OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link, FrameIntent.AskCancel));
                return true;
            case "B":
                KeyNote = Labels.HandBackArrives;
                return true;
            default:
                return false;
        }
    }

    // From no row J goes to the first and K to the last; at either end the selection stays.
    private bool Move(int step)
    {
        var rows = Walk;
        if (rows.Count == 0)
        {
            return false;
        }

        var at = rows.FindIndex(r => string.Equals(r.Address, SelectedAddress, StringComparison.Ordinal));
        var to = at < 0 ? (step > 0 ? 0 : rows.Count - 1) : Math.Clamp(at + step, 0, rows.Count - 1);
        SelectedAddress = rows[to].Address;
        Mark();
        return true;
    }

    // After a read: the selection stays on the same address, or clears when its row is gone.
    private void Reselect()
    {
        if (SelectedAddress is not null && !Shown.Any(r => string.Equals(r.Address, SelectedAddress, StringComparison.Ordinal)))
        {
            SelectedAddress = null;
        }

        Mark();
    }

    private void Mark()
    {
        foreach (var row in Shown)
        {
            row.IsSelected = string.Equals(row.Address, SelectedAddress, StringComparison.Ordinal);
        }
    }
}
