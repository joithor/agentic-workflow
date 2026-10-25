using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>One token of `GET /api/user/pats` (`TokenResponse`, `backend/onyx/server/pat/models.py`): the two fields Sindri reads to find its own old tokens.</summary>
/// <param name="Id">The token's id, the path parameter of its delete.</param>
/// <param name="Name">The token's name.</param>
internal sealed record OnyxPatInfoWire(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name);
