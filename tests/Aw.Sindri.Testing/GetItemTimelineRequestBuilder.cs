using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetItemTimeline;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetItemTimelineRequest with deterministic defaults: the first item, from the start.</summary>
public sealed class GetItemTimelineRequestBuilder
{
    private ItemRef _item = ItemRef.To(A.FirstItemId);
    private long? _after;

    /// <summary>Sets the item.</summary>
    /// <param name="value">The id or alias.</param>
    /// <returns>This builder.</returns>
    public GetItemTimelineRequestBuilder WithItem(ItemRef value)
    {
        _item = value;
        return this;
    }

    /// <summary>Sets the cursor.</summary>
    /// <param name="value">The sequence to read after, or null.</param>
    /// <returns>This builder.</returns>
    public GetItemTimelineRequestBuilder After(long? value)
    {
        _after = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetItemTimelineRequest Build() => new(_item, _after);
}
