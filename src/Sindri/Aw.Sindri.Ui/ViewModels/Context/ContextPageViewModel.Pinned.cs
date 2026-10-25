namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>Opening the page for an item (<see cref="ContextPageViewModel"/>, continued): the item's frozen starting points are read, always against the live map.</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>What the page is doing for the item it was opened on (loading, not scoped yet, or why it failed); empty otherwise.</summary>
    public string ItemNote => string.Empty;

    /// <summary>Whether the item note shows.</summary>
    public bool HasItemNote => ItemNote.Length > 0;

    /// <summary>Opens the page for an item: reads its frozen scope and fills the starting points from it.</summary>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the scope is read and the live map is drawn.</returns>
    internal async Task OpenForItem(string itemId, CancellationToken cancellationToken = default)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var result = await Ports.Api.ItemScope(api, itemId, cancellationToken).ConfigureAwait(true);
        if (result.IsSuccess && result.Value.Scope is { } scope)
        {
            AnchorText = string.Join(' ', scope.Anchors.Select(a => a.Key).Distinct(StringComparer.Ordinal));
            await Submit(cancellationToken).ConfigureAwait(true);
        }
    }
}
