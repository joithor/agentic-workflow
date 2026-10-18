using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>
/// The Start form (screens spec §4.2, U5'): choose an approved recipe and an item, give the recipe's inputs, and send
/// StartInstance once. The subject is the item. The recipe list only picks; the inputs come from the recipe's own read.
/// A refusal is the API's code and detail, shown inline; nothing is retried by the app, and a start is marked sent only
/// when the API accepted it. Readiness is the command catalog's StartInstance row.
/// </summary>
internal sealed partial class StartFormViewModel : ObservableObject
{
    private const string Action = "StartInstance";
    private const string ItemFactName = "item.json";

    private readonly CatalogViewModel _catalog;
    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly Func<IFilePicker> _files;
    private int _form;   // which opening of the form this is; an answer is for the one that sent it
    private RecipeChoice? _chosen;   // the recipe whose inputs were asked for
    private string? _itemKey;   // the recipe's own key for its item input, when it declares one
    private bool _blocked;   // the recipes or the recipe could not be read, or there is none to start
    private bool _sentFirst;
    private bool _sentNext;

    /// <summary>The form over its ports.</summary>
    /// <param name="catalog">The command catalog: its StartInstance row says whether Start is ready.</param>
    /// <param name="url">The API now, or null when none is running.</param>
    /// <param name="api">The API.</param>
    /// <param name="files">Where a file input is chosen (read when a file is asked for).</param>
    internal StartFormViewModel(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api, Func<IFilePicker> files)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(files);
        (_catalog, _url, _api, _files) = (catalog, url, api, files);
    }

    /// <summary>Raised for every accepted start, so the shell opens the instance's frame.</summary>
    internal event EventHandler<StartedEventArgs>? Started;

    /// <summary>The app's copy for the form.</summary>
    public StartText Text { get; } = StartText.Instance;

    /// <summary>True while the form is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The approved recipes, one for each id.</summary>
    public IReadOnlyList<RecipeChoice> Recipes { get; private set => Set(ref field, value); } = [];

    /// <summary>The recipe chosen (the view calls <see cref="Choose"/> when it changes, which reads its inputs).</summary>
    public RecipeChoice? Recipe { get; set => Set(ref field, value); }

    /// <summary>The chosen recipe's inputs, as fields; an item input is the item field, not one of these.</summary>
    public IReadOnlyList<StartFieldViewModel> Fields { get; private set => Set(ref field, value); } = [];

    /// <summary>The items the queue shows, to fill the item field from.</summary>
    public IReadOnlyList<ItemChoice> Items { get; private set => Set(ref field, value); } = [];

    /// <summary>An item picked from <see cref="Items"/>; picking one fills <see cref="ItemId"/>.</summary>
    public ItemChoice? PickedItem
    {
        get;
        set
        {
            Set(ref field, value);
            if (value is not null)
            {
                ItemId = value.Id;
            }
        }
    }

    /// <summary>The item the workflow runs for.</summary>
    public string ItemId
    {
        get;
        set
        {
            Set(ref field, value);
            ClearError();
        }
    } = string.Empty;

    /// <summary>True while the recipes are being read.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); }

    /// <summary>The recipes' or the recipe's read failure as served (code and detail), the empty-list sentence, or empty.</summary>
    public string Note
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNote));
        }
    } = string.Empty;

    /// <summary>True while <see cref="Note"/> shows.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>True when the command catalog says Start is ready (a pending row, or no row, is not).</summary>
    public bool IsReady => Row is { Kind: ControlKind.Ready };

    /// <summary>For a Start that is not ready, why: "arrives in P…", or that the command list is not read; else empty.</summary>
    public string Readiness => Row switch
    {
        null => Labels.StartNoCatalog,
        { Kind: ControlKind.Ready } => string.Empty,
        { Kind: ControlKind.Pending } pending => Labels.Arrives(pending.Stage),
        _ => Labels.NotInApp,
    };

    /// <summary>True when <see cref="Readiness"/> shows.</summary>
    public bool IsNotReady => !IsReady;

    /// <summary>True while a start is in flight.</summary>
    public bool IsSending
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanStart));
            OnChanged(nameof(ButtonText));
        }
    }

    /// <summary>The Start button's words: the catalog's label, and "Starting…" while it is in flight.</summary>
    public string ButtonText => IsSending ? Labels.StartSending : Labels.StartWorkflow;

    /// <summary>True when a press would send: ready, nothing blocks it, not in flight and not already accepted.</summary>
    public bool CanStart => IsReady && !_blocked && !IsSending && !_sentFirst;

    /// <summary>The inline error: the API's code and detail as served, or the sentence of a field that cannot be sent; empty when none.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's SND code, or empty for a field's sentence.</summary>
    public string ErrorCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's detail as served, or the field's sentence.</summary>
    public string ErrorDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>Counts the errors that ask the view to move focus to the inline error.</summary>
    public int ErrorFocusRequests { get; private set => Set(ref field, value); }

    /// <summary>"already running" or "finished" for an attempt that existed; else empty.</summary>
    public string Outcome
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasOutcome));
        }
    } = string.Empty;

    /// <summary>True while <see cref="Outcome"/> shows.</summary>
    public bool HasOutcome => Outcome.Length > 0;

    /// <summary>True when the existing attempt has finished: a new attempt can be asked for.</summary>
    public bool OffersNewAttempt
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanStartNewAttempt));
        }
    }

    /// <summary>True when the new-attempt button would send: offered, not in flight and not sent yet.</summary>
    public bool CanStartNewAttempt => OffersNewAttempt && !IsSending && !_sentNext;

    private (ControlKind Kind, string Stage)? Row => _catalog.Row(Action) is { } row ? CatalogReadiness.Parse(row.Ui) : null;
}
