namespace Aw.Sindri.Core.Checks;

/// <summary>
/// The write-scope syntax of a phase packet's 'allowedFiles': a repo-relative glob, or 'new: &lt;path&gt;' for one file the phase creates. '*' and '?' stay inside one path segment;
/// a whole '**' segment matches any number of segments, none included. Nothing under '.agentic-workflow/packs' is ever allowed (line spec §5.1).
/// </summary>
public static class ScopeGlobs
{
    private const string NewPrefix = "new:";
    private const string PacksRoot = ".agentic-workflow/packs";

    /// <summary>True when one of <paramref name="entries"/> allows <paramref name="path"/>.</summary>
    /// <param name="entries">The packet's 'allowedFiles'.</param>
    /// <param name="path">A repo-relative path with '/' separators.</param>
    /// <returns>Whether the path is in scope.</returns>
    public static bool Allows(IReadOnlyList<string> entries, string path)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(path);
        if (IsPackPath(path))
        {
            return false;
        }

        var parts = path.Split('/');
        return entries.Any(entry => entry.StartsWith(NewPrefix, StringComparison.Ordinal)
            ? string.Equals(entry[NewPrefix.Length..].Trim(), path, StringComparison.Ordinal)
            : Segments(entry.Trim().Split('/'), 0, parts, 0));
    }

    /// <summary>True when <paramref name="path"/> is under '.agentic-workflow/packs', where a repo's style and lens packs live: no phase may write there (line spec §5.1).</summary>
    /// <param name="path">A repo-relative path with '/' separators.</param>
    /// <returns>Whether the path is a pack path.</returns>
    public static bool IsPackPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.Equals(path, PacksRoot, StringComparison.Ordinal) || path.StartsWith(PacksRoot + "/", StringComparison.Ordinal);
    }

    private static bool Segments(string[] glob, int g, string[] parts, int p)
    {
        if (g == glob.Length)
        {
            return p == parts.Length;
        }

        if (glob[g] == "**")
        {
            for (var skip = p; skip <= parts.Length; skip++)
            {
                if (Segments(glob, g + 1, parts, skip))
                {
                    return true;
                }
            }

            return false;
        }

        return p < parts.Length && Wildcard(glob[g], 0, parts[p], 0) && Segments(glob, g + 1, parts, p + 1);
    }

    private static bool Wildcard(string pattern, int i, string text, int t)
    {
        if (i == pattern.Length)
        {
            return t == text.Length;
        }

        if (pattern[i] == '*')
        {
            for (var rest = t; rest <= text.Length; rest++)
            {
                if (Wildcard(pattern, i + 1, text, rest))
                {
                    return true;
                }
            }

            return false;
        }

        return t < text.Length && (pattern[i] == '?' || pattern[i] == text[t]) && Wildcard(pattern, i + 1, text, t + 1);
    }
}
