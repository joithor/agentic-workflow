using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Onyx v4.9.0's ChunkInfo ('GET /api/document/chunk-info', onyx/server/documents/document.py:69-104).</summary>
/// <param name="Content">The chunk's text.</param>
/// <param name="NumTokens">Its size in tokens.</param>
internal sealed record OnyxChunkReply(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("num_tokens")] int NumTokens);
