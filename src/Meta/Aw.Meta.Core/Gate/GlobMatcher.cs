namespace Aw.Meta.Core.Gate;

/// <summary>Matches repository-relative paths against globs: "*" and "?" within a segment, "**" across segments.</summary>
public static class GlobMatcher
{
    /// <summary>Whether <paramref name="path"/> matches <paramref name="pattern"/>.</summary>
    /// <param name="pattern">The glob.</param>
    /// <param name="path">A path with "/" separators.</param>
    /// <returns>True on a match.</returns>
    public static bool IsMatch(string pattern, string path)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(path);
        return pattern.Length != 0 && Segments(pattern.Split('/'), 0, path.Split('/'), 0);
    }

    private static bool Segments(string[] pattern, int p, string[] path, int s)
    {
        for (; p < pattern.Length; p++, s++)
        {
            if (pattern[p] == "**")
            {
                return p == pattern.Length - 1 || Enumerable.Range(s, path.Length - s + 1).Any(k => Segments(pattern, p + 1, path, k));
            }

            if (s >= path.Length || !Segment(pattern[p], path[s]))
            {
                return false;
            }
        }

        return s == path.Length;
    }

    // "*" matches any run of characters (none included), "?" exactly one; the usual two-pointer match with backtracking to the last "*".
    private static bool Segment(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || pattern[p] == text[t]))
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
