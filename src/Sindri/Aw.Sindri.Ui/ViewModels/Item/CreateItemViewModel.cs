using System.ComponentModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>
/// Create item (UI spec §4.5, §7): `C` opens a small form (title, repo, body) whose button is the catalog's CreateItem
/// control, so it is drawn pending until P3 ships and keeps the §4.1 lifecycle. The new row appears from the stream's
/// re-query, never from the answer. The form closes once the accepted outcome has shown.
/// </summary>
internal sealed class CreateItemViewModel(CatalogViewModel catalog, Func<Uri?> url, ISindriApi api) : ObservableObject
{
    /// <summary>True while the form is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The title (required).</summary>
    public string Title
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(CanSubmit));
            OnChanged(nameof(IsGateOpen));
        }
    } = string.Empty;

    /// <summary>The repo, or empty for the API's default.</summary>
    public string Repo { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The body, or empty.</summary>
    public string Body { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The catalog's CreateItem control (null when the catalog has no such row).</summary>
    public ControlViewModel? Control { get; private set => Set(ref field, value); }

    /// <summary>True when the control can send and the title isn't blank.</summary>
    public bool CanSubmit => Control is { CanRun: true } && !string.IsNullOrWhiteSpace(Title);

    /// <summary>
    /// True when the form's control is usable: a ready control needs a title first; a pending or cli-only one stays usable
    /// so its phase line and Copy work (§7).
    /// </summary>
    public bool IsGateOpen => Control is not { IsReady: true } || !string.IsNullOrWhiteSpace(Title);

    /// <summary>The new item's id once accepted, else empty.</summary>
    public string CreatedId { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>`C`: opens the form, empty, with the catalog's control as it is now.</summary>
    internal void Open()
    {
        if (Control is { } old)
        {
            old.PropertyChanged -= OnControl;
        }

        Control = catalog.On("CreateItem", string.Empty, Send);
        if (Control is { } control)
        {
            control.PropertyChanged += OnControl;
        }

        (Title, Repo, Body, CreatedId) = (string.Empty, string.Empty, string.Empty, string.Empty);
        IsOpen = true;
        OnChanged(nameof(CanSubmit));
        OnChanged(nameof(IsGateOpen));
    }

    /// <summary>Esc or Cancel: closes the form; nothing is sent.</summary>
    internal void Close() => IsOpen = false;

    /// <summary>Sends the form once through the control.</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Submit(CancellationToken cancellationToken) =>
        CanSubmit && Control is { } control ? control.Press(cancellationToken) : Task.CompletedTask;

    private async Task<Result<CommandAcceptedView>> Send(CancellationToken cancellationToken)
    {
        if (url() is not { } api1)
        {
            return SindriUiErrors.NotRunning.With("no API to send to");
        }

        var request = new CreateItemRequest(Title.Trim(), Blank(Repo), Blank(Body), Link: null);
        var created = await api.CreateItem(api1, request, cancellationToken).ConfigureAwait(true);
        if (created.IsError)
        {
            return created.Error;
        }

        CreatedId = created.Value.ItemId;
        return created.Value.Accepted;
    }

    private static string? Blank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // The accepted outcome shows for its 2 s; then the form closes and empties (§4.1 lifecycle).
    private void OnControl(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ControlViewModel.CanRun))
        {
            OnChanged(nameof(CanSubmit));
        }

        if (e.PropertyName == nameof(ControlViewModel.Phase) && sender is ControlViewModel { Phase: CommandPhase.Idle } && CreatedId.Length > 0 && IsOpen)
        {
            (Title, Repo, Body) = (string.Empty, string.Empty, string.Empty);
            IsOpen = false;
        }
    }
}
