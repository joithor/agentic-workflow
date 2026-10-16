namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>Asks the view to focus a row (UI spec §3.3: the selection is the focus).</summary>
/// <param name="itemId">The row's item id.</param>
/// <param name="onlyIfLost">True when focus moves only if nothing usable has it (a refresh); false when a key asked (J / K).</param>
internal sealed class FocusRequestedEventArgs(string itemId, bool onlyIfLost) : EventArgs
{
    /// <summary>The row's item id.</summary>
    public string ItemId { get; } = itemId;

    /// <summary>True when focus moves only if nothing usable has it.</summary>
    public bool OnlyIfLost { get; } = onlyIfLost;
}
