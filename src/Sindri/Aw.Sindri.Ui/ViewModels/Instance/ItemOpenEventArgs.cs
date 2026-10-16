namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame asks to open an item's sheet (a fact that names an item, a child row).</summary>
/// <param name="itemId">The item's id.</param>
internal sealed class ItemOpenEventArgs(string itemId) : EventArgs
{
    /// <summary>The item's id.</summary>
    public string ItemId { get; } = itemId;
}
