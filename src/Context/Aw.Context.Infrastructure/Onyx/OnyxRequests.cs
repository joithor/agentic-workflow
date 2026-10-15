using System.Net.Http.Headers;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// The one HTTP send path to Onyx and the allowlist it enforces (spec §8.8): the key is an admin key, so the adapter
/// may send only its own five requests. A call whose method and route aren't on the list, or whose query could change
/// the path, is refused with an exception before anything is sent: a refused call is a bug in the adapter, not an
/// expected failure.
/// </summary>
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

    /// <summary>Sends one allowlisted call and reads the whole reply.</summary>
    /// <param name="http">The client.</param>
    /// <param name="baseUrl">Onyx's base URL, ending in '/'.</param>
    /// <param name="call">The call.</param>
    /// <param name="token">The key, sent as 'Authorization: Bearer', or null for none.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status and body.</returns>
    /// <exception cref="InvalidOperationException">The call isn't on the allowlist (nothing was sent).</exception>
    public static async Task<OnyxReply> Send(HttpClient http, Uri baseUrl, OnyxCall call, string? token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(call);
        using var request = new HttpRequestMessage(call.Method, Target(baseUrl, call));
        if (call.Body is not null)
        {
            request.Content = new ByteArrayContent(call.Body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return new OnyxReply(response.StatusCode, body);
    }

    private static Uri Target(Uri baseUrl, OnyxCall call)
    {
        var listed = Allowlist.Any(r => r.Method == call.Method && string.Equals(r.Route, call.Route, StringComparison.Ordinal));
        var plainQuery = call.Query.Length == 0 || (call.Query[0] == '?' && !call.Query.Contains('#', StringComparison.Ordinal));
        if (listed && plainQuery)
        {
            var path = new Uri(baseUrl, call.Route.TrimStart('/'));
            var target = new Uri(baseUrl, call.Route.TrimStart('/') + call.Query);
            if (string.Equals(target.AbsolutePath, path.AbsolutePath, StringComparison.Ordinal))
            {
                return target;
            }
        }

        throw new InvalidOperationException(
            $"The onyx adapter refused to send {call.Method} {call.Route}: it isn't on the adapter's allowlist "
            + $"({string.Join(", ", Allowlist.Select(r => $"{r.Method} {r.Route}"))}; spec §8.8).");
    }
}
