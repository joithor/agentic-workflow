using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>The sheet's actions (<see cref="ItemSheetViewModel"/>, continued): the item's 'actions' as the catalog's controls.</summary>
internal sealed partial class ItemSheetViewModel
{
    // The item's actions present in the catalog, in the API's order. A control lives as long as the item allows its
    // action, so its lifecycle (sending, the accepted outcome, an error) survives a refresh (§4.1).
    private void ShowActions(IReadOnlyList<string> actions)
    {
        foreach (var gone in _controls.Keys.Except(actions, StringComparer.Ordinal).ToList())
        {
            _ = _controls.Remove(gone);
        }

        var shown = new List<ControlViewModel>();
        foreach (var action in actions)
        {
            if (!_controls.TryGetValue(action, out var control) && catalog.On(action, ItemId, Sender(action)) is { } made)
            {
                control = _controls[action] = made;
            }

            if (control is not null)
            {
                shown.Add(control);
            }
        }

        if (!shown.SequenceEqual(Actions))
        {
            Actions = shown;
            OnChanged(nameof(HasActions));
        }
    }

    // What each action the sheet can send does. CloseItem's variants send the version shown at the click (§4.5); every
    // other action is drawn by the catalog (pending with its phase, or a copy block) and sends nothing from here.
    private Func<CancellationToken, Task<Result<CommandAcceptedView>>>? Sender(string action) => action switch
    {
        "CloseItem:done" => ct => Close(action, "done", ct),
        "CloseItem:cancelled" => ct => Close(action, "cancelled", ct),
        _ => null,
    };

    private async Task<Result<CommandAcceptedView>> Close(string action, string reason, CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return SindriUiErrors.NotRunning.With("no API to send to");
        }

        Problem(string.Empty, string.Empty);
        var answer = await api.CloseItem(url, ItemId, new CloseItemRequest(reason, _version), cancellationToken).ConfigureAwait(true);
        if (answer.IsError && answer.Error.Code == Conflict)
        {
            // §9.1: the item changed since the human looked. Re-read it. The pressed control shows the API's detail
            // inline while the item still allows it; when the re-read item no longer does (closed elsewhere), the
            // control is gone and the sheet shows the detail in its own line instead.
            _ = await Read(cancellationToken).ConfigureAwait(true);
            if (!_controls.ContainsKey(action))
            {
                Problem(answer.Error.Code, answer.Error.Message);
            }
        }

        return answer;
    }
}
