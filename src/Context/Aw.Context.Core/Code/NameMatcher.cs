namespace Aw.Context.Core.Code;

/// <summary>Finds a name in a file's text with word boundaries (S-CL), so a substring of a longer word is not a hit.</summary>
public static class NameMatcher
{
    /// <summary>The ways a file's path is written in an import, include or path string: its last two segments without the extension, in slash form and dotted form.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>The two forms; empty for a path with no directory.</returns>
    public static IReadOnlyList<string> PathTerms(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/');
        if (segments.Length < 2)
        {
            return [];
        }

        var stem = PathStem.Of(segments[^1]);
        var directory = segments[^2];
        return stem.Length == 0 || directory.Length == 0 ? [] : [$"{directory}/{stem}", $"{directory}.{stem}"];
    }

    /// <summary>The first path form named in the text: not preceded by a letter or digit, not followed by a letter, digit or '-'.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="terms">The path forms.</param>
    /// <returns>The form found, or null.</returns>
    public static string? FindPath(string text, IReadOnlyList<string> terms) =>
        Find(text, terms, after => !char.IsLetterOrDigit(after) && after != '-', plural: false);

    /// <summary>The first name form in the text: not preceded by a letter or digit, followed by an optional 's' or 'es' and then not by a letter or digit.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="terms">The name forms.</param>
    /// <returns>The form found, or null.</returns>
    public static string? FindTerm(string text, IReadOnlyList<string> terms) =>
        Find(text, terms, after => !char.IsLetterOrDigit(after), plural: true);

    private static string? Find(string text, IReadOnlyList<string> terms, Func<char, bool> endsWord, bool plural)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(terms);
        return terms.FirstOrDefault(term => term.Length > 0 && Occurs(text, term, endsWord, plural));
    }

    private static bool Occurs(string text, string term, Func<char, bool> endsWord, bool plural)
    {
        var from = 0;
        while (from <= text.Length - term.Length)
        {
            var at = text.IndexOf(term, from, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return false;
            }

            if ((at == 0 || !char.IsLetterOrDigit(text[at - 1])) && EndsCleanly(text, at + term.Length, endsWord, plural))
            {
                return true;
            }

            from = at + 1;
        }

        return false;
    }

    private static bool EndsCleanly(string text, int end, Func<char, bool> endsWord, bool plural)
    {
        if (Clean(text, end, endsWord))
        {
            return true;
        }

        return plural && ((char.ToLowerInvariant(text[end]) == 's' && Clean(text, end + 1, endsWord)) || (text.AsSpan(end).StartsWith("es", StringComparison.OrdinalIgnoreCase) && Clean(text, end + 2, endsWord)));
    }

    private static bool Clean(string text, int at, Func<char, bool> endsWord) => at >= text.Length || endsWord(text[at]);
}
