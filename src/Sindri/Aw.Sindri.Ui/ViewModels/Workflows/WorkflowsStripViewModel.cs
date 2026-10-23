using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// Home's Workflows strip (screens §4.1, E-20): the open root instances as the API serves them, in its order (incident, then
/// triage, then work, then oldest; the app never sorts), capped, with "… N more" that opens Workflows. The fold rule gives
/// rows back to Needs you first: the cap is what is left of the page's row budget after the Needs-you rows shown.
/// </summary>
internal sealed class WorkflowsStripViewModel(IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    /// <summary>The most rows the strip shows (screens §4.1).</summary>
    internal const int Cap = HomeFold.StripCap;

    private static readonly string[] _listProperties =
    [
        nameof(HasMore), nameof(MoreText), nameof(IsEmpty), nameof(EmptyText), nameof(IsLoading), nameof(EmptyLine), nameof(IsNotServed), nameof(NotServedText), nameof(IsUnreadable), nameof(UnreadableText), nameof(OpenText),
    ];

    private List<InstanceSummary> _open = [];
    private List<StripRowViewModel> _shown = [];
    private ListCounts? Counts { get; set; }
    private string? Empty { get; set; }
    private int NeedsYouShown { get; set; }
    private bool Stale { get; set; }

    /// <summary>Raised when "… N more" or "Open Workflows" is pressed.</summary>
    internal event EventHandler? WorkflowsRequested;

#pragma warning disable CA1822, MA0041 // a fixed sentence is a property because the view binds it
    /// <summary>The strip's title.</summary>
    public string Heading => Labels.Workflows;
#pragma warning restore CA1822, MA0041

    /// <summary>"8 open", from the list's served counts; empty before the first list.</summary>
    public string OpenText => Counts is { } counts ? InstanceHeads.Strip(counts) : string.Empty;

    /// <summary>The rows shown, in the API's order.</summary>
    public ObservableCollection<StripRowViewModel> Rows { get; } = [];

    /// <summary>True when the cap leaves open rows out.</summary>
    public bool HasMore => !IsNotServed && !IsUnreadable && OpenTotal > _shown.Count;

    // The open total is the one the server counted for the same open filter (the Workflows page's), not a count of the rows sent.
    private int OpenTotal => Counts?.Open ?? _open.Count;

    /// <summary>"… 3 more"; empty without overflow.</summary>
    public string MoreText => HasMore ? Labels.MoreRows(OpenTotal - _shown.Count) : string.Empty;

    /// <summary>True when the list is empty and served.</summary>
    public bool IsEmpty => !IsNotServed && !IsUnreadable && Counts is not null && _open.Count == 0;

    /// <summary>The served empty sentence; empty unless the strip is empty.</summary>
    public string EmptyText => IsEmpty ? Empty ?? string.Empty : string.Empty;

    /// <summary>True until the first list is read (and the route is served and readable): Home shows skeleton rows.</summary>
    public bool IsLoading => !IsNotServed && !IsUnreadable && Counts is null;

    /// <summary>The empty line Home shows: the served sentence, or the app's own words when none was served; empty unless the strip is empty.</summary>
    public string EmptyLine => !IsEmpty ? string.Empty : string.IsNullOrEmpty(Empty) ? Labels.HomeNothingRunning : Empty;

    /// <summary>True while the workflow list route is not served yet; only the strip is marked, never Home.</summary>
    public bool IsNotServed { get; private set; }

    /// <summary>What the strip says then.</summary>
    public string NotServedText => IsNotServed ? Labels.ArrivesWorkflowStrip : string.Empty;

    /// <summary>True while the workflow list answers in a shape this build can't read (an older Sindri); the strip says so instead of drawing an empty box.</summary>
    public bool IsUnreadable { get; private set; }

    /// <summary>What the strip says then.</summary>
    public string UnreadableText => IsUnreadable ? Labels.StripUnreadable : string.Empty;

    /// <summary>The text scale; at 1.3 and over the cap drops to two.</summary>
    internal double TextScale
    {
        get;
        set
        {
            field = value;
            Reshow();
        }
    } = 1.0;

    /// <summary>The selected row's address; null for none.</summary>
    internal string? SelectedAddress { get; private set; }

    /// <summary>Takes the list as the API serves it: finished instances leave, the rest keep their order.</summary>
    /// <param name="list">The list.</param>
    internal void Update(InstanceList list)
    {
        IsNotServed = false;
        IsUnreadable = false;
        Counts = list.Counts;
        Empty = list.Empty;
        _open = [.. list.Instances.Where(IsOpen)];
        if (SelectedAddress is { } selected && !_open.Exists(r => string.Equals(r.Address, selected, StringComparison.Ordinal)))
        {
            SelectedAddress = null;
            Announcement = Labels.StripDone(selected);
            OnChanged(nameof(Announcement));
        }

        Reshow();
    }

    /// <summary>The list route is not there yet (a 404 before P11): the strip says so, Home is unaffected.</summary>
    internal void MarkNotServed()
    {
        IsNotServed = true;
        IsUnreadable = false;
        _open = [];
        Counts = null;
        Reshow();
    }

    /// <summary>The list answered in a shape this build can't read: the strip says so and shows no rows (Home is unaffected).</summary>
    internal void MarkUnreadable()
    {
        IsUnreadable = true;
        IsNotServed = false;
        _open = [];
        Counts = null;
        Reshow();
    }

    /// <summary>Tells the strip how many Needs-you rows Home shows, so it gives up its rows first.</summary>
    /// <param name="count">The Needs-you rows shown.</param>
    internal void SetNeedsYouShown(int count)
    {
        NeedsYouShown = count;
        Reshow();
    }

    /// <summary>Marks the rows stale while the views are stalled.</summary>
    /// <param name="stale">True while stalled.</param>
    internal void SetStale(bool stale)
    {
        Stale = stale;
        foreach (var row in _shown)
        {
            row.SetContext(Stale, IsSelectedRow(row));
        }
    }

    /// <summary>What a screen reader hears when the selected row finished ("ITM-01JC8 done"); empty until then.</summary>
    internal string Announcement { get; private set; } = string.Empty;

    /// <summary>The shown row that is selected; null for none.</summary>
    internal StripRowViewModel? SelectedRow => _shown.Find(IsSelectedRow);

    /// <summary>Selects the shown row with this address.</summary>
    /// <param name="address">The address.</param>
    /// <returns>True when a shown row has it.</returns>
    internal bool Select(string address)
    {
        if (!_shown.Exists(r => string.Equals(r.Address, address, StringComparison.Ordinal)))
        {
            return false;
        }

        SetSelected(address);
        return true;
    }

    /// <summary>Selects no row.</summary>
    internal void Deselect() => SetSelected(address: null);

    /// <summary>Moves the selection by one shown row; with none selected it enters at the first row (down) or the last (up).</summary>
    /// <param name="step">1 for down, -1 for up.</param>
    /// <returns>False when the move would leave the strip (the shell moves on to the next panel).</returns>
    internal bool Move(int step)
    {
        var at = _shown.FindIndex(IsSelectedRow);
        var target = at < 0 ? (step < 0 ? _shown.Count - 1 : 0) : at + step;
        if (target < 0 || target >= _shown.Count)
        {
            return false;
        }

        SetSelected(_shown[target].Address);
        return true;
    }

    /// <summary>Asks to open the Workflows page.</summary>
    internal void OpenWorkflows() => WorkflowsRequested?.Invoke(this, EventArgs.Empty);

    // What is left of the fold's row budget after the Needs-you rows shown (HomeFold), at most the strip's own cap.
    private int CurrentCap => HomeFold.IsLarge(TextScale)
        ? Math.Clamp(HomeFold.LargeBudget - NeedsYouShown, 0, HomeFold.StripLargeCap)
        : Math.Clamp(HomeFold.Budget - NeedsYouShown, 0, Cap);

    // The server filters open=true (the same filter as the Workflows page); a row whose current state's cell is terminal is dropped here as a belt.
    private static bool IsOpen(InstanceSummary row) =>
        !row.States.Any(s => s.Terminal && string.Equals(s.State, row.State, StringComparison.Ordinal));

    private bool IsSelectedRow(StripRowViewModel row) => SelectedAddress is { } address && string.Equals(row.Address, address, StringComparison.Ordinal);

    private void SetSelected(string? address)
    {
        SelectedAddress = address;
        OnChanged(nameof(SelectedAddress));
        foreach (var row in _shown)
        {
            row.SetContext(Stale, IsSelectedRow(row));
        }
    }

    // A re-read serves the same instance again: its row stays (so does the control with focus); only its age is read anew.
    private StripRowViewModel Row(InstanceSummary summary, Dictionary<string, StripRowViewModel> kept)
    {
        var row = new StripRowViewModel(summary, clipboard, clock);
        if (kept.TryGetValue(row.Address, out var old) && string.Equals(old.Signature, row.Signature, StringComparison.Ordinal))
        {
            old.Refresh();
            return old;
        }

        return row;
    }

    private void Reshow()
    {
        var kept = _shown.ToDictionary(r => r.Address, StringComparer.Ordinal);
        _shown = [.. _open.Take(CurrentCap).Select(r => Row(r, kept))];
        Patch.To(Rows, _shown);
        foreach (var row in _shown)
        {
            row.SetContext(Stale, IsSelectedRow(row));
        }

        foreach (var name in _listProperties)
        {
            OnChanged(name);
        }
    }
}
