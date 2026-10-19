using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`POST /api/admin/api-key` answer: the raw key is returned once.</summary>
/// <param name="ApiKey">The key.</param>
internal sealed record OnyxKeyWire([property: JsonPropertyName("api_key")] string? ApiKey);
