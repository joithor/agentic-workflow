using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Infrastructure.Scrubbing;

/// <summary>
/// The first half of the ingest boundary (spec §7): removes HTML comments, invisible and control characters (tab and
/// newline stay), encoded blobs over 200 characters, remote images (markdown and HTML), reference-style link definitions,
/// autolinks, and link targets (the link text stays), repeating until the text is stable, at most
/// <see cref="MaxPasses"/> times. The removal list is the TS sanitizeIngest's; the implementation is its own. Every
/// removal keeps the newlines it covered, so a cleaned document has the same lines as the blob it came from and chunk
/// spans still cite the blob's own lines.
/// </summary>
internal static partial class IngestSanitizer
{
    internal const int MaxPasses = 5;

    internal static string Clean(string text)
    {
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            var next = Once(text);
            if (string.Equals(next, text, StringComparison.Ordinal))
            {
                return next;
            }

            text = next;
        }

        return text;
    }

    /// <summary>A removed span's newlines, so the line count never changes.</summary>
    /// <param name="removed">The removed text.</param>
    /// <returns>Only its newlines.</returns>
    internal static string Newlines(string removed) => new('\n', removed.Count(c => c == '\n'));

    private static string Once(string text)
    {
        var cleaned = Comment.Replace(text, static m => Newlines(m.Value));
        cleaned = HtmlImage.Replace(cleaned, static m => Newlines(m.Value));
        cleaned = Image.Replace(cleaned, string.Empty);
        cleaned = ReferenceDefinition.Replace(cleaned, string.Empty);
        cleaned = Link.Replace(cleaned, "${text}");
        cleaned = ReferenceLink.Replace(cleaned, "${text}");
        cleaned = Autolink.Replace(cleaned, string.Empty);
        cleaned = Blob.Replace(cleaned, "[encoded blob removed]");
        return Visible(cleaned);
    }

    /// <summary>Drops control, format (zero-width, bidi), private-use and unassigned characters; keeps '\n' and '\t'.</summary>
    private static string Visible(string text)
    {
        var kept = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            var hidden = rune.Value is not ('\n' or '\t') && Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned;
            if (!hidden)
            {
                _ = kept.Append(rune.ToString());
            }
        }

        return kept.ToString();
    }

    [GeneratedRegex("<!--.*?(?:-->|\\z)", RegexOptions.CultureInvariant | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Comment { get; }

    [GeneratedRegex("<img\\b[^>]{0,4000}>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlImage { get; }

    [GeneratedRegex("!\\[[^\\]\\n]{0,500}\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Image { get; }

    [GeneratedRegex("^[ ]{0,3}\\[[^\\]\\n]{1,500}\\]:[ \\t]*\\S[^\\n]{0,2000}$", RegexOptions.CultureInvariant | RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceDefinition { get; }

    [GeneratedRegex("\\[(?<text>[^\\]\\n]{0,500})\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Link { get; }

    [GeneratedRegex("\\[(?<text>[^\\]\\n]{1,500})\\]\\[[^\\]\\n]{0,500}\\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceLink { get; }

    [GeneratedRegex("<(?:https?|ftp|mailto):[^>\\s]{1,2000}>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Autolink { get; }

    [GeneratedRegex("[A-Za-z0-9+/=_-]{201,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Blob { get; }
}
