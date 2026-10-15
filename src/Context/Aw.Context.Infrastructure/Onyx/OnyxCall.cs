namespace Aw.Context.Infrastructure.Onyx;

/// <summary>One request to Onyx, before it is checked against <see cref="OnyxRequests.Allowlist"/> and sent.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Route">The route under the base URL, e.g. '/api/admin/search'.</param>
/// <param name="Query">The query string, empty or starting with '?', its values escaped (<see cref="Get"/>).</param>
/// <param name="Body">The JSON body, or null.</param>
internal sealed record OnyxCall(HttpMethod Method, string Route, string Query, byte[]? Body)
{
    /// <summary>A GET with escaped query parameters, in order.</summary>
    /// <param name="route">The route.</param>
    /// <param name="parameters">The names and values.</param>
    /// <returns>The call.</returns>
    public static OnyxCall Get(string route, params (string Name, string Value)[] parameters) =>
        new(HttpMethod.Get, route, parameters.Length == 0 ? "" : "?" + string.Join('&', parameters.Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value)}")), Body: null);

    /// <summary>A POST with a JSON body.</summary>
    /// <param name="route">The route.</param>
    /// <param name="body">The UTF-8 JSON.</param>
    /// <returns>The call.</returns>
    public static OnyxCall Post(string route, byte[] body) => new(HttpMethod.Post, route, "", body);
}
