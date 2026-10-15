using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetItem;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetItemRequest with deterministic defaults: the first item.</summary>
public sealed class GetItemRequestBuilder
{
    private ItemRef _item = ItemRef.To(A.FirstItemId);

    /// <summary>Sets the item.</summary>
    /// <param name="value">The id or alias.</param>
    /// <returns>This builder.</returns>
    public GetItemRequestBuilder WithItem(ItemRef value)
    {
        _item = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetItemRequest Build() => new(_item);
}
