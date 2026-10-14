namespace Aw.Context.Core.Trust;

/// <summary>
/// Which permalinks a result may carry (spec §7): 'https' on linear.app. Later phases add the profile's Slack domain
/// and the loopback Onyx origin; anything else is dropped, never rewritten.
/// </summary>
public static class Permalinks
{
    /// <summary>The link when it is allowed, else null.</summary>
    /// <param name="link">A source's permalink, or null.</param>
    /// <returns>The link or null.</returns>
    public static Uri? Allowed(Uri? link) =>
        link is { IsAbsoluteUri: true } && string.Equals(link.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        && string.Equals(link.IdnHost, "linear.app", StringComparison.OrdinalIgnoreCase) && link.IsDefaultPort
            ? link
            : null;
}
