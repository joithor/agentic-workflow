using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`POST /api/auth/register` body (fastapi-users `UserCreate`, `backend/onyx/auth/schemas.py`): the two fields it needs.</summary>
/// <param name="Email">The new user's email.</param>
/// <param name="Password">Its password; never put in a message or a log.</param>
internal sealed record OnyxRegisterRequestWire(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password);
