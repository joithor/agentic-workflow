using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Packets;

/// <summary>The path segments aw-packets files a packet's freeze record under (packets/&lt;owner&gt;/&lt;name&gt;/&lt;id&gt;/frozen): the repo slug and the id segment (aw-packets' PacketId).</summary>
public static partial class PacketSlugs
{
    // The longest a packet id segment may be.
    private const int PacketIdMaxLength = 64;

    private const int DefaultMaxLength = 40;

    /// <summary>The owner and the repo's name aw-packets files a packet under ('owner/name'): the profile names no remote, so the owner is 'sindri'.</summary>
    /// <param name="repo">The instance's repo, or null.</param>
    /// <returns>The slug, or null when the instance has no repo.</returns>
    public static string? RepoSlug(RepoName? repo) => repo is { } name ? "sindri/" + name.Value : null;

    /// <summary>The id segment aw-packets files a scoping freeze under (1-64 of [a-z0-9-], starting with a letter or digit): the instance's address as a slug, stable for every re-run of the instance.</summary>
    /// <param name="address">The instance's address ('T-1/scoping/1').</param>
    /// <returns>The slug.</returns>
    public static string InstanceSlug(string address) => Slug(address, PacketIdMaxLength);

    /// <summary>A packet id segment: lower-case letters, digits and '-' (aw-packets' PacketId).</summary>
    /// <param name="text">Any text.</param>
    /// <param name="maxLength">The most characters to keep (default 40).</param>
    /// <returns>The slug, 'x' when nothing is left.</returns>
    public static string Slug(string text, int maxLength = DefaultMaxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        var slug = NotId.Replace(text.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "x" : slug.Length > maxLength ? slug[..maxLength].TrimEnd('-') : slug;
    }

    [GeneratedRegex("[^a-z0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NotId { get; }
}
