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

    /// <summary>
    /// Whether the fake Onyx key holds the 'admin' permission (the default). Without it 'GET /api/me' lists a basic
    /// user's permissions and 'POST /api/admin/search' answers Onyx's 403 INSUFFICIENT_PERMISSIONS.
    /// </summary>
    public bool OnyxAdmin { get; init; } = true;

    /// <summary>How long the fake waits before it answers each request (to test a client's deadline); zero by default.</summary>
    public TimeSpan Delay { get; init; }

    /// <summary>
    /// Rewrites the JSON text of each reply the fake Onyx server sends, to plant what the fixture never holds (a
    /// secret-shaped identifier in a link, a body in a shape v4.9.0 doesn't send); null sends the replies as they are.
    /// </summary>
    public Func<string, string>? OnyxPlant { get; init; }
}
