using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>One user group of `GET /api/manage/admin/user-group`.</summary>
/// <param name="Id">The group id.</param>
/// <param name="Name">The group name (the default admin group is named "Admin").</param>
/// <param name="IsDefault">Whether Onyx made it itself.</param>
internal sealed record OnyxGroupWire(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("is_default")] bool IsDefault);

/// <summary>`POST /api/admin/api-key` body.</summary>
/// <param name="Name">The key's name.</param>
/// <param name="GroupIds">The groups its service account joins.</param>
internal sealed record OnyxKeyRequestWire(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("group_ids")] int[] GroupIds);

/// <summary>`POST /api/admin/api-key` answer: the raw key is returned once.</summary>
/// <param name="ApiKey">The key.</param>
internal sealed record OnyxKeyWire([property: JsonPropertyName("api_key")] string? ApiKey);

/// <summary>`GET /api/me` answer, the two fields Sindri reads.</summary>
/// <param name="AccountType">STANDARD or SERVICE_ACCOUNT.</param>
/// <param name="EffectivePermissions">The permission tokens ("admin" is the full admin panel).</param>
internal sealed record OnyxMeWire(
    [property: JsonPropertyName("account_type")] string? AccountType,
    [property: JsonPropertyName("effective_permissions")] string[]? EffectivePermissions);
