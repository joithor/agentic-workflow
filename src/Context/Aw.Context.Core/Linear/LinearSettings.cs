using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>
/// The profile's 'context.sources.linear' entry, checked (spec §8.8, §14 S-L2, D18): Linear's GraphQL endpoint, the secret pointer to a read-only personal API key,
/// and the team keys that scope every read. The url is Linear's own API host over https, or a loopback address (the fixture's fake server) whose token is an 'env:' or
/// 'file:' pointer, so the real keychain key never goes to a local listener. Only <see cref="Create"/> makes one. No message repeats the token, or a user name or password written into the url.
/// </summary>
public sealed record LinearSettings
{
    private const string UrlKey = "context.sources.linear.url";
    private const string TokenKey = "context.sources.linear.token";
    private const string LinearHost = "api.linear.app";
    private const string GraphQlRoute = "/graphql";
    private const int MaxShown = 64;
    private static readonly HashSet<string> _schemes = new(StringComparer.Ordinal) { "env", "file", "keychain", "op" };
    private static readonly HashSet<string> _localSchemes = new(StringComparer.Ordinal) { "env", "file" };

    private LinearSettings(Uri url, string token, IReadOnlyList<string> teams)
    {
        Url = url;
        Token = token;
        Teams = teams;
    }

    /// <summary>Linear's GraphQL endpoint.</summary>
    public Uri Url { get; }

    /// <summary>The secret pointer to the API key ('env:…', 'file:…', 'keychain:…', 'op:…'), never the key itself.</summary>
    public string Token { get; }

    /// <summary>The team keys every read is limited to; empty reads every team the key can see.</summary>
    public IReadOnlyList<string> Teams { get; }

    /// <summary>Linear's host and port, as messages name it.</summary>
    public string Address => Url.Authority;

    /// <summary>Checks the profile's values.</summary>
    /// <param name="url">'context.sources.linear.url', or null for Linear's own API.</param>
    /// <param name="token">'context.sources.linear.token', a secret pointer.</param>
    /// <param name="teams">'context.sources.linear.teams' (already checked as team keys).</param>
    /// <returns>The settings, SND-CONTEXT-070 for a url that breaks the rules, or SND-CONTEXT-002 for no token or one that isn't a secret pointer (never repeated).</returns>
    public static Result<LinearSettings> Create(string? url, string? token, IReadOnlyList<string> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        if (string.IsNullOrWhiteSpace(token))
        {
            return ContextErrors.SourceNotConfigured.With(
                $"{TokenKey}: required: a secret pointer to the Linear API key (env:NAME, file:/path, keychain:service/account or op:vault/item/field).");
        }

        if (!IsPointer(token))
        {
            return ContextErrors.SourceNotConfigured.With(
                $"{TokenKey}: isn't a secret pointer, so it isn't used or shown here. Keep the Linear API key outside the profile and point to it: "
                + "env:NAME, file:/path (mode 0600), keychain:service/account or op:vault/item/field (spec §4); rotate a key that was written into the profile.");
        }

        var endpoint = CheckUrl(url, token);
        return endpoint.IsError ? endpoint.Error : new LinearSettings(endpoint.Value, token, [.. teams]);
    }

    /// <summary>The settings without the token pointer.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"LinearSettings {{ Url = {Url}, Teams = {string.Join(',', Teams)}, Token = (withheld) }}";

    private static Result<Uri> CheckUrl(string? text, string token)
    {
        if (text is null)
        {
            return new Uri($"https://{LinearHost}{GraphQlRoute}");
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Refused("it isn't an absolute http or https URL");
        }

        return Shape(uri) is { } wrong ? Refused(wrong) : Host(uri, token);
    }

    private static bool IsRoute(string path) => string.Equals(path, "/", StringComparison.Ordinal) || string.Equals(path, GraphQlRoute, StringComparison.Ordinal);

    /// <summary>What is wrong with the url's parts other than its host, or null.</summary>
    private static string? Shape(Uri uri) => (uri.UserInfo.Length > 0, uri.Query.Length > 0 || uri.Fragment.Length > 0, IsRoute(uri.AbsolutePath)) switch
    {
        (true, _, _) => $"it carries a user name or password; Linear's API key goes behind {TokenKey}",
        (_, true, _) => "it carries a query or fragment; give Linear's GraphQL endpoint only",
        (_, _, false) => $"its path is not {GraphQlRoute}",
        _ => null,
    };

    private static Result<Uri> Host(Uri uri, string token)
    {
        var linear = string.Equals(uri.Host, LinearHost, StringComparison.OrdinalIgnoreCase);
        if (linear && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort)
        {
            return new Uri($"https://{LinearHost}{GraphQlRoute}");
        }

        if (linear || !IsLoopback(uri))
        {
            return Refused($"its host is '{Shown(uri.Host)}', not {LinearHost} over https");
        }

        return _localSchemes.Contains(Scheme(token))
            ? new Uri($"{uri.Scheme}://{uri.Authority}{GraphQlRoute}")
            : Refused($"a loopback Linear is for the fixture only, so its token must be an env: or file: pointer, not {Scheme(token)}: (the real key never goes to a local listener)");
    }

    private static bool IsLoopback(Uri uri) =>
        (uri.HostNameType == UriHostNameType.Dns && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        || (uri.HostNameType == UriHostNameType.IPv4 && uri.Host.StartsWith("127.", StringComparison.Ordinal))
        || (uri.HostNameType == UriHostNameType.IPv6 && string.Equals(uri.Host, "[::1]", StringComparison.Ordinal));

    private static Error Refused(string why) => ContextErrors.LinearUrlRefused.With(
        $"{UrlKey}: Linear must be at https://{LinearHost}, or at a loopback address (127.0.0.0/8, [::1] or localhost) with an env: or file: token, but {why} (spec §14 S-L2).");

    /// <summary>A known scheme and a non-empty target: the least that tells a pointer from a key written into the profile (Sindri's own parser decides the rest).</summary>
    private static bool IsPointer(string token)
    {
        var colon = token.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && colon < token.Length - 1 && _schemes.Contains(token[..colon]);
    }

    private static string Scheme(string pointer) => pointer[..pointer.IndexOf(':', StringComparison.Ordinal)];

    /// <summary>Text from outside (a host) as a message shows it: one line, at most 64 characters.</summary>
    private static string Shown(string text)
    {
        var line = new string([.. text.Select(c => char.IsControl(c) ? ' ' : c)]);
        return line.Length <= MaxShown ? line : line[..MaxShown] + "…";
    }
}
