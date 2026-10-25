using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// 'GET /api/me' as Onyx v4.9.0 answers an API-key caller (onyx/server/manage/users.py verify_user_logged_in,
/// manage/models.py UserInfo). 'effective_permissions' is every Permission for an admin (auth/permissions.py
/// get_effective_permissions); for a basic user it is the small set 'basic' and the CE grants imply (approximate).
/// </summary>
internal static class OnyxMe
{
    private static readonly string[] _admin =
    [
        "add:agents", "admin", "basic", "craft_sandbox", "create:user_api_keys", "generate:image", "manage:actions", "manage:agents",
        "manage:bots", "manage:connectors", "manage:document_sets", "manage:llms", "manage:service_account_api_keys", "manage:skills",
        "manage:user_groups", "read:admin", "read:agent_analytics", "read:agents", "read:chat", "read:connectors", "read:document_sets",
        "read:query_history", "read:search", "read:user_groups", "read:users", "use:llm_gateway", "write:chat",
    ];

    private static readonly string[] _basic = ["add:agents", "basic", "read:chat", "read:search", "write:chat"];

    public static JsonObject Info(string accountType, bool admin, string? email = null)
    {
        var permissions = admin ? _admin : _basic;
        return new JsonObject
        {
            ["id"] = "0e5a1b00-0000-4000-8000-00000000a001",
            ["email"] = email ?? "sindri-fixture@service-account.invalid",
            ["is_active"] = true,
            ["is_superuser"] = false,
            ["is_verified"] = true,
            ["account_type"] = accountType,
            ["preferences"] = new JsonObject(),
            ["personalization"] = new JsonObject(),
            ["token_expires_at"] = null,
            ["is_cloud_superuser"] = false,
            ["team_name"] = null,
            ["is_anonymous_user"] = null,
            ["password_configured"] = false,
            ["tenant_info"] = new JsonObject { ["new_tenant"] = null, ["invitation"] = null },
            ["effective_permissions"] = new JsonArray([.. permissions.Select(p => JsonValue.Create(p))]),
            ["is_group_manager"] = false,
            ["admin_capabilities"] = new JsonArray([.. permissions.Select(p => JsonValue.Create(p))]),
        };
    }
}
