using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Onyx v4.9.0's AdminSearchRequest (onyx/server/query_and_chat/models.py:290): both fields are required.</summary>
/// <param name="Query">The keyword query.</param>
/// <param name="Filters">The filters; only a query's updated-since goes as 'time_cutoff' (an as-of cut is applied to the hits, not sent: 'time_cutoff' means "updated since"). Admin search can't page: a reply holds at most 50 hits, which the backtest reports as 'issue listing capped'.</param>
internal sealed record OnyxAdminSearchRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("filters")] OnyxSearchFilters Filters);
