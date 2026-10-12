using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>
/// The item detail sheet (UI spec §4.6, §4.9): GET /sindri/items/{id} and its timeline. The header is the API's fields as
/// words; the actions are the item's 'actions' rendered from the catalog; Close item sends the version shown (§4.5). A
/// SND-ITEM-409 re-reads the item and shows the API's detail (§9.1); SND-ITEM-404 shows its detail with Back.
/// </summary>
internal sealed partial class ItemSheetViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock) : ObservableObject
{
    private const string Conflict = "SND-ITEM-409";
    private const string NotFound = "SND-ITEM-404";

    private readonly Dictionary<string, ControlViewModel> _controls = new(StringComparer.Ordinal);
    private Uri? _url;
    private long _version;
    private int _pages;

    /// <summary>The item's id ('ITM-…'), or empty before one is opened.</summary>
    public string ItemId { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True between Open and the first answer (§4.1: skeleton once).</summary>
    public bool IsLoading { get; private set => Set(ref field, value); }

    /// <summary>True once an item has been read.</summary>
    public bool HasItem { get; private set => Set(ref field, value); }

    /// <summary>The title as served.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The repo, or empty.</summary>
    public string Repo { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The item state as a word.</summary>
    public string StateWord { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"version N": the version Close sends.</summary>
    public string VersionText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>How long since the item entered its state.</summary>
    public string Age { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The engine step, or empty before S2.</summary>
    public string Step { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Who created it, as the API names the actor.</summary>
    public string CreatedBy { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The decision it waits on, or empty.</summary>
    public string WaitingOn { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when it waits on a decision.</summary>
    public bool HasWaitingOn => WaitingOn.Length > 0;

    /// <summary>The engine instance's address, or empty before S2.</summary>
    public string Instance { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the API names an instance.</summary>
    public bool HasInstance => Instance.Length > 0;

    /// <summary>True when the item doesn't exist (SND-ITEM-404).</summary>
    public bool IsNotFound { get; private set => Set(ref field, value); }

    /// <summary>True while a refusal the sheet owns is shown (SND-ITEM-404, SND-ITEM-409).</summary>
    public bool HasProblem => ProblemCode.Length > 0;

    /// <summary>The refusal's code (the API's title).</summary>
    public string ProblemCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's detail, as served.</summary>
    public string ProblemDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the last read failed: what's shown is the last good answer.</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>The item's actions that the catalog has, in the API's order.</summary>
    public IReadOnlyList<ControlViewModel> Actions { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the item has actions to show.</summary>
    public bool HasActions => Actions.Count > 0;

    /// <summary>The timeline entries read so far, in the API's order.</summary>
    public IReadOnlyList<TimelineRow> Timeline { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the API names a next page.</summary>
    public bool HasMore => Next is not null;

    private long? Next { get; set { field = value; OnChanged(nameof(HasMore)); } }

    /// <summary>Opens an item: reads it and its timeline's first page.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal async Task Open(Uri url, string itemId, CancellationToken cancellationToken)
    {
        if (!string.Equals(itemId, ItemId, StringComparison.Ordinal))
        {
            Clear();
            ItemId = itemId;
        }

        (_url, _pages, IsLoading) = (url, Math.Max(_pages, 1), !HasItem);
        await Read(cancellationToken).ConfigureAwait(true);
        IsLoading = false;
    }

    /// <summary>Re-reads the open item and the timeline pages read so far (on every event, §9.2).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        if (ItemId.Length == 0)
        {
            return Task.CompletedTask;
        }

        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Load more: appends the page the API's 'next' names.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the page is shown.</returns>
    internal async Task LoadMore(CancellationToken cancellationToken)
    {
        if (_url is not { } url || Next is not { } next)
        {
            return;
        }

        var page = await api.Timeline(url, ItemId, next, cancellationToken).ConfigureAwait(true);
        if (page.IsSuccess)
        {
            _pages++;
            Timeline = [.. Timeline, .. Rows(page.Value)];
            Next = page.Value.Next;
        }
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var item = await api.Item(url, ItemId, cancellationToken).ConfigureAwait(true);
        if (item.IsError)
        {
            Failed(item.Error);
            return;
        }

        Show(item.Value);
        await ReadTimeline(url, cancellationToken).ConfigureAwait(true);
    }

    // The pages read so far, from the first (a refresh never shrinks what Load more showed).
    private async Task ReadTimeline(Uri url, CancellationToken cancellationToken)
    {
        var rows = new List<TimelineRow>();
        long? after = 0;
        for (var page = 0; page < _pages && after is { } cursor; page++)
        {
            var answer = await api.Timeline(url, ItemId, cursor, cancellationToken).ConfigureAwait(true);
            if (answer.IsError)
            {
                return;
            }

            rows.AddRange(Rows(answer.Value));
            after = answer.Value.Next;
        }

        (Timeline, Next) = (rows, after);
    }

    private IEnumerable<TimelineRow> Rows(TimelineView page) => page.Entries.Select(e => new TimelineRow(
        e.Sequence, TimeZoneInfo.ConvertTime(e.Time, clock.LocalTimeZone).ToString("HH:mm:ss", CultureInfo.InvariantCulture), e.Type, e.Actor, e.Line));

    private void Show(ItemView item)
    {
        (HasItem, IsNotFound, IsStale, _version) = (true, false, false, item.Version);
        if (ProblemCode == NotFound)
        {
            Problem(string.Empty, string.Empty);
        }

        (Title, Repo, StateWord, Step) = (item.Title, item.Repo ?? string.Empty, Labels.ItemState(item.State), item.Step ?? string.Empty);
        (VersionText, Age, CreatedBy) = (Labels.Version(item.Version), Labels.Age(clock.GetUtcNow() - item.Since), item.CreatedBy);
        (WaitingOn, Instance) = (item.WaitingOn ?? string.Empty, item.Instance ?? string.Empty);
        OnChanged(nameof(HasWaitingOn));
        OnChanged(nameof(HasInstance));
        ShowActions(item.Actions);
    }

    private void Failed(Error error)
    {
        if (error.Code == NotFound)
        {
            (IsNotFound, HasItem) = (true, false);
            Problem(error.Code, error.Message);
            ShowActions([]);
            return;
        }

        IsStale = HasItem;
    }

    private void Problem(string code, string detail)
    {
        (ProblemCode, ProblemDetail) = (code, detail);
        OnChanged(nameof(HasProblem));
    }

    private void Clear()
    {
        _controls.Clear();
        (_pages, Next, HasItem, IsNotFound, IsStale) = (0, null, false, false, false);
        (Title, Repo, StateWord, VersionText, Age, Step, CreatedBy, WaitingOn, Instance) =
            (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (Timeline, Actions) = ([], []);
        Problem(string.Empty, string.Empty);
    }
}
