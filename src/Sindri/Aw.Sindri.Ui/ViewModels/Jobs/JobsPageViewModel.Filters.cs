using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The Jobs page's filters (continued): the served state and kind query parameters, and the line above a table with nothing running.</summary>
internal sealed partial class JobsPageViewModel
{
    private readonly Dictionary<string, string> _kinds = new(StringComparer.Ordinal);

    /// <summary>The column heads.</summary>
    public IReadOnlyList<string> Columns { get; } = Labels.JobColumns;

    /// <summary>The status choices; the first is no filter.</summary>
    public IReadOnlyList<string> StateChoices { get; } = Labels.JobStateChoices;

    /// <summary>The kind choices seen so far; the first is no filter.</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> KindChoices { get; } = [Labels.AllKinds];

    /// <summary>The status filter as shown; choosing one reads the list again.</summary>
    public string SelectedState
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                Set(ref field, value);
                Reread();
            }
        }
    } = Labels.JobStateChoices[0];

    /// <summary>The kind filter as shown; choosing one reads the list again.</summary>
    public string SelectedKind
    {
        get;
        set
        {
            if (!string.Equals(field, value, StringComparison.Ordinal))
            {
                Set(ref field, value);
                Reread();
            }
        }
    } = Labels.AllKinds;

    /// <summary>True when the table has jobs and none of them runs.</summary>
    public bool IsNoneRunning { get; private set => Set(ref field, value); }

    /// <summary>The line above that table.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string NoneRunningText => Labels.NoRunningJobs;
#pragma warning restore CA1822, MA0041

    /// <summary>True under 760 px: the table keeps Job, Status and Rounds.</summary>
    public bool IsCompact { get; private set => Set(ref field, value); }

    /// <summary>True when the last read failed (not unreadable): the line and Try again show, over the old table if there is one.</summary>
    public bool ShowRetry { get; private set => Set(ref field, value); }

    /// <summary>True while the old table is shown dimmed under a failed reload.</summary>
    public bool IsDimmed { get; private set => Set(ref field, value); }

    /// <summary>The skeleton rows drawn while the first read is on its way.</summary>
    public IReadOnlyList<int> SkeletonRows { get; } = [.. Enumerable.Range(0, Labels.JobsSkeletonRows)];

    /// <summary>The link beside "no jobs yet".</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string ServicesLinkText => Labels.JobsServicesLink;
#pragma warning restore CA1822, MA0041

    /// <summary>True when nothing has ever run (an empty list with no filter chosen): the link to Services shows.</summary>
    public bool ShowServicesLink => IsEmpty && string.Equals(SelectedState, Labels.JobStateChoices[0], StringComparison.Ordinal) && string.Equals(SelectedKind, Labels.AllKinds, StringComparison.Ordinal);

    /// <summary>Raised when the Services link is pressed; the shell opens System's Services tab.</summary>
    internal event EventHandler? ServicesRequested;

    /// <summary>Asks the shell for System's Services tab.</summary>
    internal void RequestServices() => ServicesRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The page's width changed: under 760 px the table keeps three columns.</summary>
    /// <param name="width">The page's width in px.</param>
    internal void Resize(double width)
    {
        var compact = width < Labels.JobsWideFrom;
        if (compact == IsCompact)
        {
            return;
        }

        IsCompact = compact;
        foreach (var row in _rows.Values)
        {
            row.SetCompact(compact);
        }
    }

    private JobQuery Query() => new(
        Labels.JobStateServed(SelectedState),
        _kinds.TryGetValue(SelectedKind, out var kind) ? kind : null);

    // A filter changed: read again now, as the catch-up read (observed through CatchUp), when the page is shown.
    private void Reread()
    {
        OnChanged(nameof(ShowServicesLink));
        if (IsActive && _url is { } url)
        {
            Dispose();
            _shown = new CancellationTokenSource();
            CatchUp = CatchUpRead(url, _shown.Token);
        }
    }

    // The kinds a read served become the kind filter's choices, so the choice list is what Sindri has run.
    private void NoteKinds(IEnumerable<JobRowView> jobs)
    {
        foreach (var kind in jobs.Select(j => j.Kind).Distinct(StringComparer.Ordinal))
        {
            var word = Labels.JobKind(kind);
            if (_kinds.TryAdd(word, kind))
            {
                KindChoices.Add(word);
            }
        }
    }
}
