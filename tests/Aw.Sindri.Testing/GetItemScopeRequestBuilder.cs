using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetItemScope;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetItemScopeRequest with deterministic defaults: the first item.</summary>
public sealed class GetItemScopeRequestBuilder
{
    private ItemRef _item = ItemRef.To(A.FirstItemId);

    /// <summary>Sets the item.</summary>
    /// <param name="value">The id or alias.</param>
    /// <returns>This builder.</returns>
    public GetItemScopeRequestBuilder WithItem(ItemRef value)
    {
        _item = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetItemScopeRequest Build() => new(_item);
}
