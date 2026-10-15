using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Onyx v4.9.0's AdminSearchResponse (onyx/server/query_and_chat/models.py:295): at most 50 documents, one per id.</summary>
/// <param name="Documents">The documents, in Onyx's order.</param>
internal sealed record OnyxAdminSearchReply([property: JsonPropertyName("documents")] IReadOnlyList<OnyxSearchDoc> Documents);
