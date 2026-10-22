namespace Aw.Sindri.Features.Docs;

/// <summary>A doc path glob (profile key 'workflow.docs.globs'): '/' separates segments, '**' as a whole segment spans any number of them (none included), '*' and '?' match inside one segment.</summary>
internal static class DocGlob
{
    /// <summary>Whether the path matches the glob.</summary>
    /// <param name="glob">The glob.</param>
    /// <param name="path">The path relative to the repo root, with '/' separators.</param>
    /// <returns>True when it matches.</returns>
    internal static bool Matches(string glob, string path) => Segments(glob.Split('/'), 0, path.Split('/'), 0);

    private static bool Segments(string[] glob, int g, string[] path, int p)
    {
        if (g == glob.Length)
        {
            return p == path.Length;
        }

        if (string.Equals(glob[g], "**", StringComparison.Ordinal))
        {
            return Enumerable.Range(p, path.Length - p + 1).Any(next => Segments(glob, g + 1, path, next));
        }

        return p < path.Length && Name(glob[g], 0, path[p], 0) && Segments(glob, g + 1, path, p + 1);
    }

    private static bool Name(string pattern, int i, string text, int t)
    {
        if (i == pattern.Length)
        {
            return t == text.Length;
        }

        return pattern[i] == '*'
            ? Enumerable.Range(t, text.Length - t + 1).Any(next => Name(pattern, i + 1, text, next))
            : t < text.Length && (pattern[i] == '?' || pattern[i] == text[t]) && Name(pattern, i + 1, text, t + 1);
    }
}
