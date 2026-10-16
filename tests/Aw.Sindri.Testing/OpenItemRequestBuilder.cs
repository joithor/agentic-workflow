using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.OpenItem;

namespace Aw.Sindri.Testing;

/// <summary>Builds a OpenItemRequest with deterministic defaults.</summary>
public sealed class OpenItemRequestBuilder
{
    private ItemId _itemId = ItemId.From("itemid-1");

    /// <summary>Sets ItemId.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public OpenItemRequestBuilder WithItemId(ItemId value)
    {
        _itemId = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public OpenItemRequest Build() => new(_itemId);
}
