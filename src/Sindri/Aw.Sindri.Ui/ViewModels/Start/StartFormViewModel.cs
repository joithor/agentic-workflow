using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>
/// The Start form (screens spec §4.2, U5'): choose an approved recipe and an item, give the recipe's inputs, and send
/// StartInstance once. The subject is the item. The recipe list only picks; the inputs come from the recipe's own read.
/// A refusal is the API's code and detail, shown inline; nothing is retried by the app.
/// </summary>
internal sealed class StartFormViewModel : ObservableObject
{
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
    public ItemChoice? PickedItem { get; set => Set(ref field, value); }

    /// <summary>The item the workflow runs for.</summary>
    public string ItemId { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the recipes are being read.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); }

    /// <summary>The recipes' or the recipe's read failure as served (code and detail), the empty-list sentence, or empty.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while <see cref="Note"/> shows.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>True when the command catalog says Start is ready (a pending row is not).</summary>
    public bool IsReady { get; private set => Set(ref field, value); }

    /// <summary>For a pending Start, "arrives in P…"; else empty.</summary>
    public string Readiness { get; } = string.Empty;

    /// <summary>True while a start is in flight.</summary>
    public bool IsSending { get; private set => Set(ref field, value); }

    /// <summary>True when a press would send: ready, not in flight and not already accepted.</summary>
    public bool CanStart => IsReady && !IsSending;

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
    public string Outcome { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while <see cref="Outcome"/> shows.</summary>
    public bool HasOutcome => Outcome.Length > 0;

    /// <summary>True when the existing attempt has finished: a new attempt can be asked for.</summary>
    public bool OffersNewAttempt { get; private set => Set(ref field, value); }

    /// <summary>Opens the form: reads the recipes and the queue's items.</summary>
    /// <param name="itemId">The item to start for, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when both reads are shown.</returns>
    internal Task Open(string? itemId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ItemId = itemId ?? string.Empty;
        throw new NotSupportedException();
    }

    /// <summary>Esc or Cancel: closes the form; nothing is sent.</summary>
    internal void Close() => IsOpen = false;

    /// <summary>Chooses a recipe and reads its inputs.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the fields are shown.</returns>
    internal Task Choose(RecipeChoice recipe, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Recipe = recipe;
        throw new NotSupportedException();
    }

    /// <summary>Sends the form once.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Submit(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Error = string.Empty;
        throw new NotSupportedException();
    }

    /// <summary>Sends the form again as the next attempt: a second, explicit press, offered only after a finished attempt was shown.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task StartNewAttempt(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Started?.Invoke(this, new StartedEventArgs(string.Empty, string.Empty, existing: false, finished: false));
        throw new NotSupportedException();
    }
}
