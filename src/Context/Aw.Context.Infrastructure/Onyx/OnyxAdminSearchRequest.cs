using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Onyx v4.9.0's AdminSearchRequest (onyx/server/query_and_chat/models.py:290): both fields are required.</summary>
/// <param name="Query">The keyword query.</param>
/// <param name="Filters">The filters; none are set (an as-of cut is applied to the hits, not sent: 'time_cutoff' means "updated since").</param>
internal sealed record OnyxAdminSearchRequest(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("filters")] OnyxSearchFilters Filters);
