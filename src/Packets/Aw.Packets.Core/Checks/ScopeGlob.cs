using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Checks;

/// <summary>
/// One write-scope entry: a repo-relative glob, or 'new: &lt;path&gt;' for a file the task creates. In a glob, '*' and '?'
/// stay inside one path segment and a whole '**' segment matches any number of segments (none included). Matching is
/// a two-pointer walk over segments and characters: no regex, bounded by the entry's length with no backtracking
/// blow-up.
/// </summary>
internal sealed class ScopeGlob
{
    /// <summary>The longest entry accepted; a longer one is a PKT-SCOPE-001 failure.</summary>
    internal const int MaxLength = 1024;

    private const string NewPrefix = "new:";
    private const string AnySegments = "**";
    private readonly string[]? _segments;

    private ScopeGlob(string entry, string? newPath, string[]? segments)
    {
        Entry = entry;
        NewPath = newPath;
        _segments = segments;
    }

    /// <summary>The entry as written.</summary>
    public string Entry { get; }

    /// <summary>The path a 'new:' entry declares; null for a glob.</summary>
    public string? NewPath { get; }

    /// <summary>Parses one entry; null when it is empty, too long, or a 'new:' path isn't a safe relative path.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The scope, or null.</returns>
    public static ScopeGlob? Parse(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Length > MaxLength)
        {
            return null;
        }

        if (entry.StartsWith(NewPrefix, StringComparison.Ordinal))
        {
            var path = entry[NewPrefix.Length..].Trim();
            return RelativePath.IsSafe(path) ? new ScopeGlob(entry, path, segments: null) : null;
        }

        var glob = entry.Trim();
        return glob.Length == 0 ? null : new ScopeGlob(entry, newPath: null, glob.Split('/'));
    }

    /// <summary>True when the glob matches <paramref name="path"/>, or a 'new:' entry declares exactly it.</summary>
    /// <param name="path">A repo-relative path with '/' separators.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(string path) => Matches(path, path.Split('/'));

    /// <summary>As <see cref="Matches(string)"/>, with the path already split at '/'.</summary>
    /// <param name="path">The path.</param>
    /// <param name="segments">Its segments.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(string path, string[] segments) =>
        _segments is null ? string.Equals(NewPath, path, StringComparison.Ordinal) : MatchSegments(_segments, segments);

    // '**' over segments, like '*' over characters: on a mismatch, retry from the last '**' one segment later.
    private static bool MatchSegments(string[] glob, string[] path)
    {
        int g = 0, p = 0, star = -1, resume = 0;
        while (p < path.Length)
        {
            if (g < glob.Length && glob[g] == AnySegments)
            {
                (star, resume) = (g++, p);
            }
            else if (g < glob.Length && MatchSegment(glob[g], path[p]))
            {
                (g, p) = (g + 1, p + 1);
            }
            else if (star >= 0)
            {
                (g, p) = (star + 1, ++resume);
            }
            else
            {
                return false;
            }
        }

        while (g < glob.Length && glob[g] == AnySegments)
        {
            g++;
        }

        return g == glob.Length;
    }

    // '*' (any run) and '?' (one character) inside one segment.
    private static bool MatchSegment(string glob, string text)
    {
        int g = 0, t = 0, star = -1, resume = 0;
        while (t < text.Length)
        {
            if (g < glob.Length && glob[g] == '*')
            {
                (star, resume) = (g++, t);
            }
            else if (g < glob.Length && (glob[g] == '?' || glob[g] == text[t]))
            {
                (g, t) = (g + 1, t + 1);
            }
            else if (star >= 0)
            {
                (g, t) = (star + 1, ++resume);
            }
            else
            {
                return false;
            }
        }

        while (g < glob.Length && glob[g] == '*')
        {
            g++;
        }

        return g == glob.Length;
    }
}
