namespace Aw.Context.Testing.Fakes;

/// <summary>Options for a fake server.</summary>
public sealed record FakeServerOptions
{
    /// <summary>The credential the server accepts; null takes the server's default fixture token.</summary>
    public string? Token { get; init; }

    /// <summary>The version the fake Onyx server's 'GET /api/version' reports; null takes <see cref="FakeOnyxServer.PinnedVersion"/>.</summary>
    public string? OnyxVersion { get; init; }
}
