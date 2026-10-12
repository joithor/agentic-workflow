using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Infrastructure.Scrubbing;

/// <summary>
/// The first half of the ingest boundary (spec §7): removes HTML comments, invisible and control characters (tab and
/// newline stay), encoded blobs over 200 characters, remote images, and link targets (the link text stays), repeating
/// until the text is stable, at most <see cref="MaxPasses"/> times. What it removes is the TS sanitizeIngest's list; how is
/// its own.
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

    private static string Once(string text)
    {
        var cleaned = Comment.Replace(text, string.Empty);
        cleaned = Image.Replace(cleaned, string.Empty);
        cleaned = Link.Replace(cleaned, "${text}");
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

    [GeneratedRegex("!\\[[^\\]\\n]{0,500}\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Image { get; }

    [GeneratedRegex("\\[(?<text>[^\\]\\n]{0,500})\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Link { get; }

    [GeneratedRegex("[A-Za-z0-9+/=_-]{201,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Blob { get; }
}
