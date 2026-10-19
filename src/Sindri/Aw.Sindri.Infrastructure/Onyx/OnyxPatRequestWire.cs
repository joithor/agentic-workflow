using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`POST /api/user/pats` body (`CreateTokenRequest`, `backend/onyx/server/pat/models.py`). Both optional fields are sent as null: no expiry, and no scopes, which is a token with the user's full permissions.</summary>
/// <param name="Name">The token's name.</param>
/// <param name="ExpirationDays">Null: no expiry.</param>
/// <param name="Scopes">Null: unscoped.</param>
internal sealed record OnyxPatRequestWire(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("expiration_days")] int? ExpirationDays,
    [property: JsonPropertyName("scopes")] string[]? Scopes);
