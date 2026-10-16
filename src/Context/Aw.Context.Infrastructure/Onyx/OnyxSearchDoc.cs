using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// The fields of Onyx v4.9.0's SearchDoc (onyx/context/search/models.py:302-327) the adapter reads. 'score' is
/// discarded (Sindri ranks, D3); 'blurb' and 'match_highlights' are left to fetch.
/// </summary>
/// <param name="DocumentId">The document id (a Linear issue's UUID, a Slack '&lt;channel&gt;__&lt;ts&gt;').</param>
/// <param name="SemanticIdentifier">The connector-built title ('[ENG-482] Title' for Linear).</param>
/// <param name="Link">The document's link, if any.</param>
/// <param name="SourceType">The connector ('linear', 'slack', …).</param>
/// <param name="UpdatedAt">When the source last changed the document, if known (UTC).</param>
/// <param name="PrimaryOwners">The document's primary owners, if any (the first is the author).</param>
/// <param name="Hidden">Whether an Onyx admin hid the document. Only admin search returns hidden documents (it retrieves
/// with include_hidden); the source drops them, so Sindri sees what a standard search would.</param>
internal sealed record OnyxSearchDoc(
    [property: JsonPropertyName("document_id")] string DocumentId,
    [property: JsonPropertyName("semantic_identifier")] string SemanticIdentifier,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("source_type")] string SourceType,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt,
    [property: JsonPropertyName("primary_owners")] IReadOnlyList<string>? PrimaryOwners,
    [property: JsonPropertyName("hidden")] bool Hidden)
{
    /// <summary>The connector's metadata as Onyx returns it (strings or lists); 'created_at' gives a Linear issue's creation time.</summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, System.Text.Json.JsonElement>? Metadata { get; init; }

    /// <summary>The issue's creation time from the connector metadata, or null when it names none that parses.</summary>
    [JsonIgnore]
    public DateTimeOffset? CreatedAt =>
        Metadata is not null && Metadata.TryGetValue("created_at", out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
        && DateTimeOffset.TryParse(value.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var at)
            ? at
            : null;
}
