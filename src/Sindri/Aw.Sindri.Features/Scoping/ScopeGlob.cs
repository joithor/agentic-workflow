namespace Aw.Sindri.Features.Scoping;

/// <summary>The glob form of a scope map's excluded[] entry: '**' crosses directories, '*' and '?' stay inside one path segment. Matched against a node key or its path without the 'code:' prefix.</summary>
internal static class ScopeGlob
{
    private const string CodePrefix = "code:";

    /// <summary>Whether any glob matches the node key (or the path after 'code:').</summary>
    /// <param name="globs">The excluded[] globs.</param>
    /// <param name="key">The node key.</param>
    /// <returns>True when one matches.</returns>
    internal static bool MatchesAny(IReadOnlyList<string> globs, string key)
    {
        if (globs.Count == 0)
        {
            return false;
        }

        var path = key.StartsWith(CodePrefix, StringComparison.Ordinal) ? key[CodePrefix.Length..] : key;
        return globs.Any(g => Match(g, 0, key, 0) || Match(g, 0, path, 0));
    }

    private static bool Match(string glob, int g, string text, int t)
    {
        while (g < glob.Length)
        {
            var c = glob[g];
            if (c == '*')
            {
                var deep = g + 1 < glob.Length && glob[g + 1] == '*';
                var next = g + (deep ? 2 : 1);
                if (deep && next < glob.Length && glob[next] == '/')
                {
                    next++;   // '**/' also matches zero directories
                }

                for (var end = t; end <= text.Length; end++)
                {
                    if (Match(glob, next, text, end))
                    {
                        return true;
                    }

                    if (end < text.Length && !deep && text[end] == '/')
                    {
                        break;
                    }
                }

                return false;
            }

            if (t >= text.Length || (c != '?' && c != text[t]) || (c == '?' && text[t] == '/'))
            {
                return false;
            }

            g++;
            t++;
        }

        return t == text.Length;
    }
}
