namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// Selection, focus and the polite region (<see cref="QueueViewModel"/>, continued; UI spec §3.3): one selection is the
/// focus, J / K walk the page's own rows, a refresh keeps the selection by id and, when its row cleared, moves it to the
/// next row, says so once and asks for focus there; a filter change that hides the selection clears it silently.
/// </summary>
internal sealed partial class QueueViewModel
{
    /// <summary>Selects a row by id (a click, focus arriving on it); focus is already there.</summary>
    /// <param name="itemId">The row's id.</param>
    internal void Select(string itemId)
    {
        SelectedId = itemId;
        Mark();
    }

    /// <summary>
    /// J / K (§8): the next or previous row of the page the key was pressed on, in the order it draws them; stays at the
    /// ends. Selecting is focusing (§3.3): the view is asked to move focus onto the row.
    /// </summary>
    /// <param name="by">+1 for next, -1 for previous.</param>
    /// <param name="scope">The page the key was pressed on.</param>
    internal void Move(int by, QueueScope scope = QueueScope.Page)
    {
        var rows = Navigable(scope);
        if (rows.Count == 0)
        {
            return;
        }

        var at = rows.FindIndex(r => string.Equals(r.Id, SelectedId, StringComparison.Ordinal));
        var next = at < 0 ? 0 : Math.Clamp(at + by, 0, rows.Count - 1);
        Select(rows[next].Id);
        FocusRequested?.Invoke(this, new FocusRequestedEventArgs(rows[next].Id, onlyIfLost: false));
    }

    /// <summary>True when the selected row is one the page draws, so Enter and Y act on a row the user can see.</summary>
    /// <param name="scope">The page the key was pressed on.</param>
    /// <returns>True when there is a selected row on the page.</returns>
    internal bool HasSelectionOn(QueueScope scope) => Navigable(scope).Exists(r => string.Equals(r.Id, SelectedId, StringComparison.Ordinal));

    // The rows a page draws, in order: Home's two panels, or the Queue page's visible groups that are open.
    private List<ItemRowViewModel> Navigable(QueueScope scope) => scope == QueueScope.Home
        ? [.. RunningRows, .. QueuedRows]
        : [.. Groups.Where(g => g.IsVisible && g.IsExpanded).SelectMany(g => g.Rows)];

    // A filter change that hides the selected row clears it: nothing hidden stays selected for Enter or Y (no announcement:
    // the human did it).
    private void ClearHidden()
    {
        if (SelectedId is { } id && !Visible().Any(r => string.Equals(r.Id, id, StringComparison.Ordinal)))
        {
            SelectedId = null;
            Mark();
        }
    }

    // A row hidden by the filter is not gone: it is checked against the unfiltered view.
    private void Keep(List<(string Id, string Group)> before)
    {
        if (SelectedId is not { } id || _rows.ContainsKey(id))
        {
            return;
        }

        var next = Successor(id, before);
        Say(Labels.Cleared(id));
        SelectedId = next?.Id;
        _movedAt = clock.GetUtcNow();
        Mark();
        if (next is not null)
        {
            FocusRequested?.Invoke(this, new FocusRequestedEventArgs(next.Id, onlyIfLost: true));
        }
    }

    // The row now at the cleared row's place in the same group, else the group's last row, else the first visible row.
    private ItemRowViewModel? Successor(string id, List<(string Id, string Group)> before)
    {
        var old = before.FirstOrDefault(r => r.Id == id);
        var group = old.Id is null ? null : Groups.FirstOrDefault(g => g.Group == old.Group && g.IsVisible && g.Rows.Count > 0);
        var index = old.Id is null ? 0 : before.Where(r => r.Group == old.Group).ToList().FindIndex(r => r.Id == id);
        return group is null ? Visible().FirstOrDefault() : group.Rows[Math.Min(index, group.Rows.Count - 1)];
    }

    private void Mark()
    {
        foreach (var row in Groups.SelectMany(g => g.Rows))
        {
            row.IsSelected = string.Equals(row.Id, SelectedId, StringComparison.Ordinal);
        }
    }

    // Says a text through the polite region: dark first, the resting text set, then live, then the text (the flag changes
    // before the text, as the shell's regions do), so the resting text is never spoken and a text is spoken once.
    private void Say(string text)
    {
        if (IsAnnouncing && string.Equals(Announcement, text, StringComparison.Ordinal))
        {
            IsAnnouncing = false;   // the same text again is still a change: dark first, then the text
        }

        if (!IsAnnouncing)
        {
            Announcement = Labels.Sindri;
            IsAnnouncing = true;
        }

        Announcement = text;
    }

    // The region goes dark again at the next refresh (flag first, then the resting text).
    private void Rest()
    {
        if (IsAnnouncing)
        {
            IsAnnouncing = false;
            Announcement = Labels.Sindri;
        }
    }
}
