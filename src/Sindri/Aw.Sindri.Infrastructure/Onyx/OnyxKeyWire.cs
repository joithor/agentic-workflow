using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>
/// `POST /api/admin/api-key` answer: Onyx's `ApiKeyDescriptor` (`backend/onyx/auth/api_key.py` at the pinned commit f1738ed). The raw key is returned once; `groups` is
/// `get_user_groups(…, include_default=True)` of the key's service account, read back after the insert, so it says which groups the account really joined.
/// </summary>
/// <param name="ApiKey">The key.</param>
/// <param name="ApiKeyDisplay">The masked key Onyx shows in its admin page; names the key when the owner has to delete it.</param>
/// <param name="Groups">The groups of the key's service account (`UserGroupInfo`: id and name only, no default flag).</param>
internal sealed record OnyxKeyWire(
    [property: JsonPropertyName("api_key")] string? ApiKey,
    [property: JsonPropertyName("api_key_display")] string? ApiKeyDisplay = null,
    [property: JsonPropertyName("groups")] OnyxGroupRefWire[]? Groups = null);
