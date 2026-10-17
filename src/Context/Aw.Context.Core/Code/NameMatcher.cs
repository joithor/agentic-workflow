namespace Aw.Context.Core.Code;

/// <summary>Finds a name in a file's text with word boundaries (S-CL), so a substring of a longer word is not a hit.</summary>
public static class NameMatcher
{
    /// <summary>The ways a file's path is written in an import, include or path string: its last two segments without the extension, in slash form and dotted form.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <returns>The two forms; empty for a path with no directory.</returns>
    public static IReadOnlyList<string> PathTerms(string path) => throw new NotSupportedException();

    /// <summary>The first path form named in the text: not preceded by a letter or digit, not followed by a letter, digit or '-'.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="terms">The path forms.</param>
    /// <returns>The form found, or null.</returns>
    public static string? FindPath(string text, IReadOnlyList<string> terms) => throw new NotSupportedException();

    /// <summary>The first name form in the text: not preceded by a letter or digit, followed by an optional 's' or 'es' and then not by a letter or digit.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="terms">The name forms.</param>
    /// <returns>The form found, or null.</returns>
    public static string? FindTerm(string text, IReadOnlyList<string> terms) => throw new NotSupportedException();
}
