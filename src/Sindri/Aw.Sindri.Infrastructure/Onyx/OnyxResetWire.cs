using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`POST /api/password/reset_password` answer (`UserResetResponse`): the new password, which exists nowhere else.</summary>
/// <param name="NewPassword">The password.</param>
internal sealed record OnyxResetWire([property: JsonPropertyName("new_password")] string? NewPassword);
