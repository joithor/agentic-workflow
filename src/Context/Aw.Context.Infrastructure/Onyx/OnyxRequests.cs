namespace Aw.Context.Infrastructure.Onyx;

/// <summary>The one HTTP send path to Onyx, and the allowlist it enforces (spec §8.8).</summary>
internal static class OnyxRequests
{
    /// <summary>The five requests the onyx adapter makes; nothing else is ever sent.</summary>
    public static IReadOnlyList<OnyxRoute> Allowlist { get; } =
    [
        new(HttpMethod.Post, "/api/admin/search"),
        new(HttpMethod.Get, "/api/document/document-size-info"),
        new(HttpMethod.Get, "/api/document/chunk-info"),
        new(HttpMethod.Get, "/api/version"),
        new(HttpMethod.Get, "/api/me"),
    ];

    /// <summary>Sends one allowlisted call.</summary>
    /// <param name="http">The client.</param>
    /// <param name="baseUrl">Onyx's base URL.</param>
    /// <param name="call">The call.</param>
    /// <param name="token">The key, sent as 'Bearer', or null for no Authorization header.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reply.</returns>
    public static Task<OnyxReply> Send(HttpClient http, Uri baseUrl, OnyxCall call, string? token, CancellationToken cancellationToken)
    {
        _ = (http, baseUrl, call, token, cancellationToken);
        throw new NotSupportedException("red: the Onyx send path");
    }
}
