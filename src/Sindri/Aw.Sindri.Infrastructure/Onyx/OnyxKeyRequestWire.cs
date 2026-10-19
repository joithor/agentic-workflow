using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`POST /api/admin/api-key` body.</summary>
/// <param name="Name">The key's name.</param>
/// <param name="GroupIds">The groups its service account joins.</param>
internal sealed record OnyxKeyRequestWire(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("group_ids")] int[] GroupIds);
