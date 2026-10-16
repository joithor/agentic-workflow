using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>The id the item builders default to.</summary>
    public static ItemId FirstItemId { get; } = ItemId.From("ITM-00000000000000000000000000000001");

    /// <summary>An items-view row builder: a new item at version 1.</summary>
    /// <returns>A new builder.</returns>
    public static ItemRowBuilder Item() => new();

    /// <summary>A caller context builder: a session over HTTP unless told otherwise.</summary>
    /// <returns>A new builder.</returns>
    public static CallerContextBuilder Caller() => new();

    /// <summary>A ListCommandsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ListCommandsRequestBuilder ListCommandsRequest() => new();

    /// <summary>A ListServicesRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ListServicesRequestBuilder ListServicesRequest() => new();

    /// <summary>A GetProfileRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetProfileRequestBuilder GetProfileRequest() => new();

    /// <summary>A RebuildViewsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static RebuildViewsRequestBuilder RebuildViewsRequest() => new();

    /// <summary>A CreateItemRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static CreateItemRequestBuilder CreateItemRequest() => new();

    /// <summary>A CloseItemRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static CloseItemRequestBuilder CloseItemRequest() => new();

    /// <summary>A GetQueueRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetQueueRequestBuilder GetQueueRequest() => new();

    /// <summary>A ListItemsRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ListItemsRequestBuilder ListItemsRequest() => new();

    /// <summary>A GetItemRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetItemRequestBuilder GetItemRequest() => new();

    /// <summary>A GetItemTimelineRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetItemTimelineRequestBuilder GetItemTimelineRequest() => new();

    /// <summary>A ReadLedgerRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static ReadLedgerRequestBuilder ReadLedgerRequest() => new();

    /// <summary>A GetEventCatalogRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static GetEventCatalogRequestBuilder GetEventCatalogRequest() => new();
}
