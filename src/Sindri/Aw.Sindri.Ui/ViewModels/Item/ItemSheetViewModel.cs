using System.Collections.ObjectModel;
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
    private bool _readProblem;
    private long _version;
    private int _pages;

    /// <summary>The app's copy for the sheet's headings and buttons.</summary>
    public ItemText Text { get; } = ItemText.Instance;

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

    /// <summary>The item's body as served (scrubbed by the API; plain text), or empty (§4.6).</summary>
    public string Body { get; private set { Set(ref field, value); OnChanged(nameof(HasBody)); } } = string.Empty;

    /// <summary>True when the item has a body.</summary>
    public bool HasBody => Body.Length > 0;

    /// <summary>The tracker record key the item is linked to, or empty (§4.6).</summary>
    public string Link { get; private set { Set(ref field, value); OnChanged(nameof(HasLink)); } } = string.Empty;

    /// <summary>True when the item is linked.</summary>
    public bool HasLink => Link.Length > 0;

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
    public ObservableCollection<TimelineRow> Timeline { get; } = [];

    /// <summary>True when the API names a next page.</summary>
    public bool HasMore => Next is not null;

    private long? Next { get; set { field = value; OnChanged(nameof(HasMore)); } }

    private void Show(ItemView item)
    {
        (HasItem, IsNotFound, IsStale, _version) = (true, false, false, item.Version);
        if (ProblemCode == NotFound || _readProblem)
        {
            _readProblem = false;
            Problem(string.Empty, string.Empty);
        }

        (Title, Repo, StateWord, Step) = (item.Title, item.Repo ?? string.Empty, Labels.ItemState(item.State), item.Step ?? string.Empty);
        (VersionText, Age, CreatedBy) = (Labels.Version(item.Version), Labels.Age(clock.GetUtcNow() - item.Since), item.CreatedBy);
        (WaitingOn, Instance) = (item.WaitingOn ?? string.Empty, item.Instance ?? string.Empty);
        (Body, Link) = (item.Body ?? string.Empty, item.Link ?? string.Empty);
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
        if (!HasItem)
        {
            // Nothing was ever read: say what the API said (until a read succeeds) rather than a skeleton or a blank sheet.
            _readProblem = true;
            Problem(error.Code, error.Message);
        }
    }

    private void Problem(string code, string detail)
    {
        (ProblemCode, ProblemDetail) = (code, detail);
        OnChanged(nameof(HasProblem));
    }

    private void Clear()
    {
        _controls.Clear();
        _generation++;
        (_pages, Next, HasItem, IsNotFound, IsStale) = (0, null, false, false, false);
        (Title, Repo, StateWord, VersionText, Age, Step, CreatedBy, WaitingOn, Instance) =
            (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        Timeline.Clear();
        (Actions, Body, Link) = ([], string.Empty, string.Empty);
        Problem(string.Empty, string.Empty);
    }
}
