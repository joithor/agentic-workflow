using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// GET /sindri/queue as the Queue page (UI spec §4.5) and Home's Running and With you and Queued panels (§3.2, §4.2).
/// Groups, their order, the rows' order, the empty sentences and the actions are the API's (§2 "no logic"); the app maps
/// them to words and visibility, caps Home's panels at five rows, and keeps the selected row by id across refreshes (§3.3).
/// </summary>
internal sealed class QueueViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock) : ObservableObject
{
    // Home's panels show at most this many rows, then "Show all N in Queue" (§3.2 caps).
    internal const int PanelCap = 5;

    private QueueView? _view;

    /// <summary>The groups in the API's order (empty ones included, hidden by <see cref="QueueGroupViewModel.IsVisible"/>).</summary>
    public IReadOnlyList<QueueGroupViewModel> Groups { get; private set => Set(ref field, value); } = [];

    /// <summary>How many items the queue holds across its groups (the Queue tab's count).</summary>
    public int Total { get; private set => Set(ref field, value); }

    /// <summary>True when no group has a row and no filter is on: the page shows the view's sentence.</summary>
    public bool IsEmpty { get; private set => Set(ref field, value); }

    /// <summary>The view's own empty sentence, as served.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True until the first answer (§4.1: skeleton rows once, never on refresh).</summary>
    public bool IsLoading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when the last read failed: the rows shown are the last good ones (§4.1 Stale).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>Home's left panel: With you first, then running, at most five (§3.2).</summary>
    public IReadOnlyList<ItemRowViewModel> RunningRows { get; private set => Set(ref field, value); } = [];

    /// <summary>"RUNNING N · WITH YOU N".</summary>
    public string RunningHeader { get; private set => Set(ref field, value); } = Labels.RunningHeader(0, 0);

    /// <summary>The running group's sentence while the panel has no row (E-18), else empty.</summary>
    public string RunningEmpty { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the panel holds more rows than it shows.</summary>
    public bool HasMoreRunning { get; private set => Set(ref field, value); }

    /// <summary>"Show all N in Queue" for the left panel.</summary>
    public string ShowAllRunning { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Home's right panel: the queued group's first five rows.</summary>
    public IReadOnlyList<ItemRowViewModel> QueuedRows { get; private set => Set(ref field, value); } = [];

    /// <summary>"QUEUED N".</summary>
    public string QueuedHeader { get; private set => Set(ref field, value); } = Labels.QueuedHeader(0);

    /// <summary>The queued group's sentence while it has no row (E-18), else empty.</summary>
    public string QueuedEmpty { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the queued group holds more rows than the panel shows.</summary>
    public bool HasMoreQueued { get; private set => Set(ref field, value); }

    /// <summary>"Show all N in Queue" for the right panel.</summary>
    public string ShowAllQueued { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The selected row's id, kept across refreshes; null when none.</summary>
    public string? SelectedId { get; private set => Set(ref field, value); }

    /// <summary>The polite announcement when the selected row cleared ("ITM-… cleared"), else empty.</summary>
    public string Announcement { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The filter the page shows (G Q, G R, G W).</summary>
    public QueueFilter Showing { get; private set; }

    /// <summary>Re-reads the view (on connect and on every event, §9.2) and shows it; a failure keeps the last rows, stale.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the view is shown.</returns>
    internal async Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        var answer = await api.Queue(url, cancellationToken).ConfigureAwait(true);
        if (answer.IsError)
        {
            IsStale = _view is not null;
            return;
        }

        var before = Visible().ToList();
        _view = answer.Value;
        (IsLoading, IsStale) = (false, false);
        Show();
        Keep(before);
    }

    /// <summary>Shows one group (G R, G W) or all of them (G Q).</summary>
    /// <param name="filter">The filter.</param>
    internal void Filter(QueueFilter filter)
    {
        Showing = filter;
        Show();
    }

    /// <summary>Selects a row by id.</summary>
    /// <param name="itemId">The row's id.</param>
    internal void Select(string itemId)
    {
        SelectedId = itemId;
        Mark();
    }

    /// <summary>J / K (§8): the next or previous visible row, in the API's order; stays at the ends.</summary>
    /// <param name="by">+1 for next, -1 for previous.</param>
    internal void Move(int by)
    {
        var rows = Visible().ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var at = rows.FindIndex(r => string.Equals(r.Id, SelectedId, StringComparison.Ordinal));
        var next = at < 0 ? 0 : Math.Clamp(at + by, 0, rows.Count - 1);
        Select(rows[next].Id);
    }

    private IEnumerable<ItemRowViewModel> Visible() => Groups.Where(g => g.IsVisible).SelectMany(g => g.Rows);

    private void Show()
    {
        if (_view is not { } view)
        {
            return;
        }

        var now = clock.GetUtcNow();
        Groups = [.. view.Groups.Select(g => Group(g, now))];
        Total = Groups.Sum(g => g.Count);
        IsEmpty = Showing == QueueFilter.All && Total == 0;
        EmptyText = IsEmpty ? view.Empty ?? string.Empty : string.Empty;
        Panels();
        Mark();
    }

    private QueueGroupViewModel Group(QueueGroupView group, DateTimeOffset now)
    {
        var rows = group.Items.Select(i => new ItemRowViewModel(i, group.Group, Primary(i), now)).ToList();
        var picked = Showing switch
        {
            QueueFilter.Running => group.Group == "running",
            QueueFilter.WithYou => group.Group == "withYou",
            QueueFilter.All => true,
            _ => true,
        };
        return new QueueGroupViewModel(group.Group, rows, group.Empty, picked) { IsFiltered = picked && Showing != QueueFilter.All };
    }

    // The row's primary action as the catalog's control. A row sends nothing itself: its item actions need the version the
    // sheet shows (CloseItem) or arrive in later phases, so the control is drawn (pending with its phase) and Enter opens the sheet.
    private ControlViewModel? Primary(ItemSummaryView item) =>
        item.PrimaryAction is { } action ? catalog.On(action, item.Id, send: null) : null;

    private void Panels()
    {
        var withYou = Rows("withYou");
        var running = Rows("running");
        var queued = Rows("queued");
        var left = withYou.Concat(running).ToList();
        RunningRows = [.. left.Take(PanelCap)];
        RunningHeader = Labels.RunningHeader(running.Count, withYou.Count);
        RunningEmpty = left.Count == 0 ? EmptyOf("running") : string.Empty;
        (HasMoreRunning, ShowAllRunning) = (left.Count > PanelCap, Labels.ShowAllInQueue(left.Count));
        QueuedRows = [.. queued.Take(PanelCap)];
        QueuedHeader = Labels.QueuedHeader(queued.Count);
        QueuedEmpty = queued.Count == 0 ? EmptyOf("queued") : string.Empty;
        (HasMoreQueued, ShowAllQueued) = (queued.Count > PanelCap, Labels.ShowAllInQueue(queued.Count));
    }

    private List<ItemRowViewModel> Rows(string group) =>
        Groups.FirstOrDefault(g => g.Group == group)?.Rows.ToList() ?? [];

    private string EmptyOf(string group) => Groups.FirstOrDefault(g => g.Group == group)?.Empty ?? string.Empty;

    // §3.3: a refresh keeps the selected row by id; if it cleared, the selection moves to the row now at its place in the
    // same group (else the group's last row, else the first visible row) and the clearing is announced.
    private void Keep(List<ItemRowViewModel> before)
    {
        Announcement = string.Empty;
        if (SelectedId is not { } id || Visible().Any(r => r.Id == id))
        {
            return;
        }

        var old = before.FirstOrDefault(r => r.Id == id);
        var group = old is null ? null : Groups.FirstOrDefault(g => g.Group == old.Group && g.IsVisible && g.Rows.Count > 0);
        var index = old is null ? 0 : before.Where(r => r.Group == old.Group).ToList().IndexOf(old);
        var next = group is null ? Visible().FirstOrDefault() : group.Rows[Math.Min(index, group.Rows.Count - 1)];
        Announcement = Labels.Cleared(id);
        SelectedId = next?.Id;
        Mark();
    }

    private void Mark()
    {
        foreach (var row in Groups.SelectMany(g => g.Rows))
        {
            row.IsSelected = string.Equals(row.Id, SelectedId, StringComparison.Ordinal);
        }
    }
}
