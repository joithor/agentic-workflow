namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>One group of the queue (UI spec §4.5): its heading, its rows in the API's order, and its own empty sentence (E-18).</summary>
internal sealed class QueueGroupViewModel : ObservableObject
{
    internal QueueGroupViewModel(string group, IReadOnlyList<ItemRowViewModel> rows, string? empty, bool shown)
    {
        Group = group;
        Label = Labels.Group(group);
        Rows = rows;
        Empty = empty;
        IsShown = shown;
        IsExpanded = true;
    }

    /// <summary>The API's group name.</summary>
    public string Group { get; }

    /// <summary>The group's heading.</summary>
    public string Label { get; }

    /// <summary>The heading with its count, as the section header reads ("Queued 4").</summary>
    public string Header => Label + " " + Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>How many items the group holds.</summary>
    public int Count => Rows.Count;

    /// <summary>The rows, in the API's order.</summary>
    public IReadOnlyList<ItemRowViewModel> Rows { get; }

    /// <summary>The group's own sentence when it's empty, as served.</summary>
    public string? Empty { get; }

    /// <summary>
    /// True when the page shows the group: one with rows on the full queue, or the one the filter picked (§3.3), which
    /// shows its own sentence when empty. An empty group is hidden on the full queue (§4.5).
    /// </summary>
    public bool IsVisible => IsShown && (Rows.Count > 0 || IsFiltered);

    /// <summary>True when a filtered, empty group shows its own sentence.</summary>
    public bool ShowsEmpty => IsVisible && Rows.Count == 0;

    /// <summary>True when the section is open (collapsible, §4.5); rows show only then.</summary>
    public bool IsExpanded { get; set => Set(ref field, value); }

    /// <summary>The filter allows this group.</summary>
    internal bool IsShown { get; }

    /// <summary>The page is filtered to this one group.</summary>
    internal bool IsFiltered { get; init; }
}
