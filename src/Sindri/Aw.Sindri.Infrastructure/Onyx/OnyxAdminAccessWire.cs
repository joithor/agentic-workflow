using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`PATCH /api/manage/admin/users/admin-access` body (`UserAdminAccessUpdateRequest`, `backend/onyx/server/manage/models.py`).</summary>
/// <param name="UserEmail">The user.</param>
/// <param name="IsAdmin">Whether the user is to be an admin.</param>
internal sealed record OnyxAdminAccessWire(
    [property: JsonPropertyName("user_email")] string UserEmail,
    [property: JsonPropertyName("is_admin")] bool IsAdmin);
