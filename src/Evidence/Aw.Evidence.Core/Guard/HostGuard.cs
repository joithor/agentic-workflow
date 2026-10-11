using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Evidence.Core.Guard;

/// <summary>
/// Decides which host a run may drive before anything touches the network. Accepted: the default
/// http://localhost:3000; a loopback origin http://localhost:&lt;port&gt; or http://127.0.0.1:&lt;port&gt; (local); a
/// per-PR preview https://pr-&lt;n&gt;.vitalize.build with --allow-preview-host. Every --host is a bare origin with no
/// credentials, path, query or fragment. Everything else (dev, staging, prod) is refused.
/// </summary>
public static partial class HostGuard
{
    /// <summary>The default host: the local stack.</summary>
    public const string DefaultOrigin = "http://localhost:3000";

    /// <summary>Resolves the --host flag.</summary>
    /// <param name="host">The flag's value; null for the default.</param>
    /// <param name="allowPreviewHost">Whether --allow-preview-host was given.</param>
    /// <returns>The host, or AW-EVIDENCE-004 saying why it is refused.</returns>
    public static Result<ResolvedHost> Resolve(string? host, bool allowPreviewHost)
    {
        if (host is null)
        {
            return new ResolvedHost(DefaultOrigin, HostKind.Local);
        }

        if (!Uri.TryCreate(host, UriKind.Absolute, out var url) || url.HostNameType is UriHostNameType.Unknown)
        {
            return Refused($"--host is not a URL: {host}");
        }

        return BareOriginProblem(url, host) is { } problem
            ? Refused(problem)
            : IsLoopback(url) ? Loopback(url) : Preview(url, allowPreviewHost);
    }

    /// <summary>True when <paramref name="origin"/> is this machine (localhost or 127.0.0.1).</summary>
    /// <param name="origin">An origin or URL.</param>
    /// <returns>Whether it is local.</returns>
    public static bool IsLocal(string origin) => Uri.TryCreate(origin, UriKind.Absolute, out var url) && IsLoopback(url);

    /// <summary>True for a navigation request that would take the browser off <paramref name="origin"/> (about: pages stay).</summary>
    /// <param name="origin">The vetted origin.</param>
    /// <param name="url">The requested URL.</param>
    /// <param name="isNavigation">Whether the request is a navigation.</param>
    /// <returns>Whether to abort it. An unparseable URL is aborted.</returns>
    public static bool IsOffOriginNavigation(string origin, string url, bool isNavigation)
    {
        if (!isNavigation || url.StartsWith("about:", StringComparison.Ordinal))
        {
            return false;
        }

        return !Uri.TryCreate(url, UriKind.Absolute, out var target)
            || !Uri.TryCreate(origin, UriKind.Absolute, out var vetted)
            || Uri.Compare(target, vetted, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0;
    }

    /// <summary>Names where <paramref name="url"/> is, for a message: its origin, its scheme when it has no host (data:), never its path or query.</summary>
    /// <param name="url">A page's URL.</param>
    /// <returns>The origin, '&lt;scheme&gt;:', or 'an unparseable URL'.</returns>
    public static string OriginOf(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return "an unparseable URL";
        }

        return parsed.Authority.Length > 0 ? parsed.GetLeftPart(UriPartial.Authority) : parsed.Scheme + ":";
    }

    private static string? BareOriginProblem(Uri url, string text)
    {
        if (url.UserInfo.Length > 0)
        {
            return "--host must not carry credentials";
        }

        var extra = url.AbsolutePath != "/" || url.Query.Length > 0 || url.Fragment.Length > 0 || text.EndsWith('?') || text.EndsWith('#');
        return extra ? "--host must be a bare origin (no path, query or fragment)" : null;
    }

    private static Result<ResolvedHost> Loopback(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttp || url.IsDefaultPort)
        {
            return Refused($"a loopback --host must be http://{url.Host}:<port>");
        }

        return new ResolvedHost(string.Create(CultureInfo.InvariantCulture, $"http://{url.Host}:{url.Port}"), HostKind.Local);
    }

    private static Result<ResolvedHost> Preview(Uri url, bool allowPreviewHost)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
        {
            return Refused("a non-loopback --host must be https");
        }

        if (!PreviewHost.IsMatch(url.Host))
        {
            return Refused($"--host {url.Host} is not a pr-<n>.vitalize.build preview host; dev and prod hosts are refused");
        }

        if (!url.IsDefaultPort)
        {
            return Refused("a preview --host must not carry a port");
        }

        return allowPreviewHost
            ? new ResolvedHost($"https://{url.Host}", HostKind.Preview)
            : Refused("--host needs --allow-preview-host");
    }

    private static bool IsLoopback(Uri url) =>
        string.Equals(url.Host, "localhost", StringComparison.Ordinal) || string.Equals(url.Host, "127.0.0.1", StringComparison.Ordinal);

    private static Error Refused(string message) => EvidenceErrors.HostRefused.With(message);

    // Per-PR previews only; dev, staging and prod hosts under the same domain never match.
    [GeneratedRegex("^pr-[0-9]+\\.vitalize\\.build\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex PreviewHost { get; }
}
