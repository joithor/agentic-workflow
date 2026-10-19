using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>`GET /api/me` answer (`UserInfo`), the fields Sindri reads.</summary>
/// <param name="AccountType">STANDARD or SERVICE_ACCOUNT.</param>
/// <param name="EffectivePermissions">The permission tokens ("admin" is the full admin panel).</param>
/// <param name="Email">The account's email.</param>
internal sealed record OnyxMeWire(
    [property: JsonPropertyName("account_type")] string? AccountType,
    [property: JsonPropertyName("effective_permissions")] string[]? EffectivePermissions,
    [property: JsonPropertyName("email")] string? Email = null);
