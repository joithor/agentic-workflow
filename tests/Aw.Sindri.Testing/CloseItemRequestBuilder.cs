using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Features.CloseItem;

namespace Aw.Sindri.Testing;

/// <summary>Builds a CloseItemRequest with deterministic defaults: the first item, done, at version 1.</summary>
public sealed class CloseItemRequestBuilder
{
    private ItemRef _item = ItemRef.To(A.FirstItemId);
    private CloseReason _reason = CloseReason.Done;
    private long _version = 1;

    /// <summary>Sets the item.</summary>
    /// <param name="value">The id or alias.</param>
    /// <returns>This builder.</returns>
    public CloseItemRequestBuilder WithItem(ItemRef value)
    {
        _item = value;
        return this;
    }

    /// <summary>Sets the reason.</summary>
    /// <param name="value">Done or cancelled.</param>
    /// <returns>This builder.</returns>
    public CloseItemRequestBuilder WithReason(CloseReason value)
    {
        _reason = value;
        return this;
    }

    /// <summary>Sets the version the human saw.</summary>
    /// <param name="value">The version.</param>
    /// <returns>This builder.</returns>
    public CloseItemRequestBuilder WithVersion(long value)
    {
        _version = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public CloseItemRequest Build() => new(_item, _reason, _version);
}
