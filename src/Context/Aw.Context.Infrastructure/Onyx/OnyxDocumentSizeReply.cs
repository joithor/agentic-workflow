using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Onyx v4.9.0's DocumentInfo ('GET /api/document/document-size-info', onyx/server/documents/document.py:25-66).</summary>
/// <param name="NumChunks">How many chunks the document has.</param>
/// <param name="NumTokens">Its size in the embedding model's tokens.</param>
internal sealed record OnyxDocumentSizeReply(
    [property: JsonPropertyName("num_chunks")] int NumChunks,
    [property: JsonPropertyName("num_tokens")] int NumTokens);
