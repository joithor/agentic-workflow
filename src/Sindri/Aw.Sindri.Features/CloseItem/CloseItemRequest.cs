using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Features.CloseItem;

/// <summary>Close an item as done or cancelled at the version the human saw (POST /sindri/items/{itemId}/close, 'sindri item close'; API spec §3.2, UI spec E-8).</summary>
/// <param name="Item">The item, by id or alias.</param>
/// <param name="Reason">Done or cancelled: the catalog variant.</param>
/// <param name="ItemVersion">The version the human saw; a stale one is SND-ITEM-409.</param>
public sealed record CloseItemRequest(ItemRef Item, CloseReason Reason, long ItemVersion) : ICommand<CommandAccepted>, IClassifiedCommand
{
    /// <inheritdoc/>
    public string Variant => Reason == CloseReason.Cancelled ? "cancelled" : "done";

    /// <inheritdoc/>
    public bool CarriesProtectedFields => false;
}
