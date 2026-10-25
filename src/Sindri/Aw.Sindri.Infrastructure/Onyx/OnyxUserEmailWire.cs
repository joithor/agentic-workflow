using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`POST /api/password/reset_password` body (`UserResetRequest`, `backend/onyx/server/features/password/models.py`).</summary>
/// <param name="UserEmail">The user whose password is reset.</param>
internal sealed record OnyxUserEmailWire([property: JsonPropertyName("user_email")] string UserEmail);
