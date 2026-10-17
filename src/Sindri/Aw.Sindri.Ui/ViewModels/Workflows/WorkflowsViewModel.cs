using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page (screens §4.2): the open root instances grouped by recipe, as the API serves them. Group order, row order, the counts
/// and the empty sentence are the API's; the app groups the rows by their recipe id, keeps what the human collapsed, and maps fields to words.
/// </summary>
internal sealed partial class WorkflowsViewModel(ISindriApi api, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    private readonly Dictionary<string, RecipeGroupViewModel> _groups = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InstanceRowViewModel> _rows = new(StringComparer.Ordinal);
    private Uri? Url { get; set; }
    private int _reads;
    private int _doneReads;

    /// <summary>Raised when a row asks to open its instance.</summary>
    internal event EventHandler<OpenRequestedEventArgs>? OpenRequested;

    /// <summary>Raised when a group head asks to open its recipe's sheet.</summary>
    internal event EventHandler<RecipeRequestedEventArgs>? RecipeRequested;

    /// <summary>The groups, in the order the API first served their recipes.</summary>
    public ObservableCollection<RecipeGroupViewModel> Groups { get; } = [];

    /// <summary>The recipes the list has shown, kept while one is chosen so the filter can go back.</summary>
    public ObservableCollection<string> KnownRecipes { get; } = [];

    /// <summary>The recipe filter's choices: all recipes, then each known recipe.</summary>
    public ObservableCollection<string> RecipeChoices { get; } = [];

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The stale note's words.</summary>
    public string StaleText => Labels.Stale;
#pragma warning restore CA1822, MA0041

    /// <summary>The legend line at the foot of the list (screens §4.2): the marks the rows draw, then the page's keys.</summary>
    public string LegendText { get; } = WorkflowsLegend.Text;

    /// <summary>The legend in words, for screen readers: no glyph.</summary>
    public string LegendName { get; } = WorkflowsLegend.Name;

    /// <summary>"8 open · 2 need you · 1 with you", root-wide whatever the filters; empty before the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The nav tab's count: the open figure; empty before the first list.</summary>
    public string NavCount { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when finished instances exist.</summary>
    public bool HasDone { get; private set => Set(ref field, value); }

    /// <summary>"4 done today", from the served finished count (provisional until the API serves a per-day figure, Q-API-3).</summary>
    public string DoneText { get; private set { Set(ref field, value); OnChanged(nameof(DoneToggleName)); } } = string.Empty;

    /// <summary>The finished line's name for screen readers: the figure and whether the rows show.</summary>
    public string DoneToggleName => $"{DoneText}, {(IsDoneExpanded ? Labels.GroupExpanded : Labels.GroupCollapsed)}";

    /// <summary>True while the finished rows show.</summary>
    public bool IsDoneExpanded { get; private set { Set(ref field, value); OnChanged(nameof(DoneToggleName)); } }

    /// <summary>The finished root rows, once the line is expanded.</summary>
    public ObservableCollection<InstanceRowViewModel> DoneRows { get; } = [];

    /// <summary>True when the list is empty and served.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The served empty sentence (the app's own when a filter leaves no row and none was served); empty unless the page is empty.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the list answers in a shape this build can't read (an older Sindri).</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>What the page says then: the same sentence as Home's strip.</summary>
    public string UnreadableText => IsUnreadable ? Labels.StripUnreadable : string.Empty;

    /// <summary>True while the views are stalled or rebuilding.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>The recipe the list is narrowed to; null for all.</summary>
    public string? RecipeFilter { get; private set => Set(ref field, value); }

    /// <summary>The subject the list is narrowed to; null for all.</summary>
    public string? SubjectFilter { get; private set => Set(ref field, value); }

    /// <summary>How many lists this page has drawn: the page's own reads, which the strip's identical query can't stand in for.</summary>
    internal int Drawn { get; private set; }

    /// <summary>Re-reads the list (on connect and on every event).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        Url = url;
        var read = ++_reads;
        _doneReads++;   // a finished read for the filter before this one (or from before this refresh) is no longer wanted
        var list = await api.Instances(url, new InstanceQuery(Root: true, Open: true, Recipe: RecipeFilter, Subject: SubjectFilter), cancellationToken).ConfigureAwait(true);
        if (read != _reads)
        {
            return;   // a newer read (a filter, an event) was asked for since: its answer is the one to show
        }

        if (list.IsSuccess)
        {
            Show(list.Value);
            if (IsDoneExpanded)
            {
                await ReadDone(cancellationToken).ConfigureAwait(true);
            }
        }
        else if (string.Equals(list.Error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) && list.Error.Kind == ErrorKind.Invalid)
        {
            MarkUnreadable();
        }
    }

    /// <summary>Narrows the list to one recipe, or to all.</summary>
    /// <param name="recipe">A recipe id, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task FilterRecipe(string? recipe, CancellationToken cancellationToken)
    {
        RecipeFilter = string.IsNullOrEmpty(recipe) ? null : recipe;
        return Reread(cancellationToken);
    }

    /// <summary>Narrows the list to one subject, or to all.</summary>
    /// <param name="subject">A subject key or label, or null.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task FilterSubject(string? subject, CancellationToken cancellationToken)
    {
        SubjectFilter = string.IsNullOrEmpty(subject) ? null : subject;
        return Reread(cancellationToken);
    }

    /// <summary>Expands the finished line (reading the finished root rows), or collapses it.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the line is shown.</returns>
    internal async Task ToggleDone(CancellationToken cancellationToken)
    {
        IsDoneExpanded = !IsDoneExpanded;
        if (IsDoneExpanded)
        {
            await ReadDone(cancellationToken).ConfigureAwait(true);
        }
        else
        {
            _doneReads++;   // a read on its way is no longer wanted
            DoneRows.Clear();
        }
    }

    /// <summary>Marks the rows stale while the views are stalled or rebuilding.</summary>
    /// <param name="stale">True while stale.</param>
    internal void SetStale(bool stale)
    {
        IsStale = stale;
        foreach (var row in _rows.Values.Concat(DoneRows))
        {
            row.SetStale(stale || IsUnreadable);
        }
    }

    /// <summary>Asks to open a row's instance.</summary>
    /// <param name="address">The instance's address.</param>
    internal void Open(string address)
    {
        if (Groups.SelectMany(g => g.Rows).Concat(DoneRows).FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal)) is { } row)
        {
            OpenRequested?.Invoke(this, new OpenRequestedEventArgs(row.Link));
        }
    }

    private Task Reread(CancellationToken cancellationToken) => Url is { } url ? Refresh(url, cancellationToken) : Task.CompletedTask;

    private void Show(InstanceList list)
    {
        Drawn++;
        var before = WalkAddresses();
        IsUnreadable = false;
        OnChanged(nameof(UnreadableText));
        (Head, NavCount) = (InstanceHeads.Workflows(list.Counts), InstanceHeads.Nav(list.Counts));
        (HasDone, DoneText) = (list.Counts.Done > 0, list.Counts.Done > 0 ? InstanceHeads.DoneLine(list.Counts.Done) : string.Empty);
        var rows = list.Instances.Select(Row).ToList();
        _rows.Clear();
        rows.ForEach(r => _rows[r.Address] = r);
        var shown = rows.GroupBy(r => r.Recipe, StringComparer.Ordinal).Select(g => Group(g.Key, [.. g])).ToList();
        Patch.To(Groups, shown);
        if (RecipeFilter is null && SubjectFilter is null)
        {
            ReplaceIfChanged(KnownRecipes, [.. shown.Select(g => g.Recipe)]);
            ReplaceIfChanged(RecipeChoices, [Labels.AllRecipes, .. KnownRecipes]);
        }

        Reselect(before);
        IsEmpty = rows.Count == 0;
        EmptyText = IsEmpty ? list.Empty ?? Labels.NoWorkflowsMatch : string.Empty;
    }

    // A choice list the human is picking from keeps its items (and the control its selection) unless the recipes themselves changed.
    private static void ReplaceIfChanged(ObservableCollection<string> list, IReadOnlyList<string> target)
    {
        if (list.SequenceEqual(target, StringComparer.Ordinal))
        {
            return;
        }

        list.Clear();
        foreach (var item in target)
        {
            list.Add(item);
        }
    }

    private RecipeGroupViewModel Group(string recipe, List<InstanceRowViewModel> rows)
    {
        if (!_groups.TryGetValue(recipe, out var group))
        {
            group = new RecipeGroupViewModel(recipe);
            group.RecipeRequested += (_, e) => RecipeRequested?.Invoke(this, e);
            _groups[recipe] = group;
        }

        group.Show(rows);
        return group;
    }

    // A re-read serves the same row again: it stays (so does the control with focus) unless something it draws changed.
    private InstanceRowViewModel Row(InstanceSummary summary)
    {
        var row = new InstanceRowViewModel(summary, clipboard, clock);
        var old = Groups.SelectMany(g => g.Rows).FirstOrDefault(r => string.Equals(r.Address, row.Address, StringComparison.Ordinal));
        var kept = old is not null && string.Equals(old.Signature, row.Signature, StringComparison.Ordinal) ? old : row;
        kept.SetStale(IsStale);
        return kept;
    }

    private async Task ReadDone(CancellationToken cancellationToken)
    {
        if (Url is not { } url)
        {
            return;
        }

        var read = ++_doneReads;
        var all = await api.Instances(url, new InstanceQuery(Root: true, Recipe: RecipeFilter, Subject: SubjectFilter), cancellationToken).ConfigureAwait(true);
        if (read != _doneReads || !IsDoneExpanded)
        {
            return;   // newer, or the line was collapsed while this was on its way
        }

        if (all.IsSuccess)
        {
            var before = WalkAddresses();
            var finished = all.Value.Instances.Where(r => r.States.Any(s => s.Terminal && string.Equals(s.State, r.State, StringComparison.Ordinal))).Select(r => new InstanceRowViewModel(r, clipboard, clock)).ToList();
            finished.ForEach(r => r.SetStale(IsStale));
            DoneRows.Clear();
            finished.ForEach(DoneRows.Add);
            Reselect(before);
        }
    }

    private void MarkUnreadable()
    {
        IsUnreadable = true;
        OnChanged(nameof(UnreadableText));
        // The last good data stays, marked stale (screens §4.1): the next readable answer rebuilds nothing the human was on.
        foreach (var row in _rows.Values.Concat(DoneRows))
        {
            row.SetStale(stale: true);
        }
    }
}
