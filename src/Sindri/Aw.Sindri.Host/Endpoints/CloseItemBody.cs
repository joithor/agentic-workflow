using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Features.CloseItem;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/items/{itemId}/close: '{reason, itemVersion}'; the item comes from the route.</summary>
/// <param name="Reason">'done' or 'cancelled'.</param>
/// <param name="ItemVersion">The version the human saw (required: a missing one is a 400).</param>
internal sealed record CloseItemBody(CloseReason Reason, long ItemVersion)
{
    /// <summary>The request for <paramref name="item"/>.</summary>
    /// <param name="item">The route's item.</param>
    /// <returns>The request.</returns>
    internal CloseItemRequest ToRequest(ItemRef item) => new(item, Reason, ItemVersion);
}
