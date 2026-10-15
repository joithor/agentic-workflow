using System.Text.Json.Serialization;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// The fields of Onyx v4.9.0's UserInfo ('GET /api/me', onyx/server/manage/users.py:1081, manage/models.py:165) that
/// show the key's role: its account type and whether its effective permissions hold 'admin'. The email is not read.
/// </summary>
/// <param name="AccountType">'SERVICE_ACCOUNT', 'STANDARD', 'BOT' or 'EXT_PERM_USER' (onyx/db/enums.py AccountType).</param>
/// <param name="EffectivePermissions">The permission tokens the account holds ('admin' for a full admin).</param>
internal sealed record OnyxMeReply(
    [property: JsonPropertyName("account_type")] string AccountType,
    [property: JsonPropertyName("effective_permissions")] IReadOnlyList<string> EffectivePermissions);
