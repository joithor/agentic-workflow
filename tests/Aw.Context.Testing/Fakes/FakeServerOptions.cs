namespace Aw.Context.Testing.Fakes;

/// <summary>Options for a fake server.</summary>
public sealed record FakeServerOptions
{
    /// <summary>The credential the server accepts; null takes the server's default fixture token.</summary>
    public string? Token { get; init; }

    /// <summary>The version the fake Onyx server's 'GET /api/version' reports; null takes <see cref="FakeOnyxServer.PinnedVersion"/>.</summary>
    public string? OnyxVersion { get; init; }

    /// <summary>
    /// The account the fake Onyx key belongs to, as 'GET /api/me' reports it ('account_type'): 'SERVICE_ACCOUNT' (the
    /// default), 'STANDARD', 'BOT' or 'EXT_PERM_USER'.
    /// </summary>
    public string? OnyxAccountType { get; init; }

    /// <summary>The account's email as 'GET /api/me' reports it ('email'); null takes the fixture's service-account address.</summary>
    public string? OnyxAccountEmail { get; init; }

    /// <summary>
    /// Whether the fake Onyx key holds the 'admin' permission (the default). Without it 'GET /api/me' lists a basic
    /// user's permissions and 'POST /api/admin/search' answers Onyx's 403 INSUFFICIENT_PERMISSIONS.
    /// </summary>
    public bool OnyxAdmin { get; init; } = true;

    /// <summary>How long the fake waits before it answers each request (to test a client's deadline); zero by default.</summary>
    public TimeSpan Delay { get; init; }

    /// <summary>
    /// Rewrites the JSON text of each reply the fake Onyx server sends, to plant what the fixture never holds (a
    /// secret-shaped identifier in a link, a hidden document, another reported version); it must return valid JSON. Null sends
    /// the replies as they are.
    /// </summary>
    public Func<string, string>? OnyxPlant { get; init; }

    /// <summary>A replacement document set (test-only): when set, the server serves these synthetic issues instead of the recorded split-panes documents.</summary>
    public IReadOnlyList<SyntheticIssue>? OnyxDocuments { get; init; }

    /// <summary>Extra recorded-looking projects the fake Linear server adds (newer than the recordings), to page and to crowd a scope with; zero by default.</summary>
    public int LinearExtraProjects { get; init; }

    /// <summary>The team key the extra projects belong to ('ENG' by default).</summary>
    public string LinearExtraProjectsTeam { get; init; } = "ENG";

    /// <summary>How many comments the fake Linear server adds to issue SPL-105 on top of the recorded ones; zero by default.</summary>
    public int LinearExtraComments { get; init; }

    /// <summary>Whether the fake Linear server serves a connection newest first, as Linear does for orderBy updatedAt and createdAt (the default keeps the oldest first, which the paging tests of the fake itself rely on).</summary>
    public bool LinearNewestFirst { get; init; }

    /// <summary>How the fake Linear server rate limits every request: none (the default), HTTP 429, or a GraphQL RATELIMITED error.</summary>
    public LinearRateLimit LinearRateLimit { get; init; }
}
