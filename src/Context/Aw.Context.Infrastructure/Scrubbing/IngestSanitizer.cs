using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Infrastructure.Scrubbing;

/// <summary>
/// The first half of the ingest boundary (spec §7): removes HTML comments, invisible and control characters (tab and
/// newline stay), encoded blobs over 200 characters, images (markdown inline and reference-style, wherever the
/// definition is, and HTML '&lt;img&gt;' and '&lt;image&gt;' tags), every other HTML tag that can fetch or point at a
/// remote resource (one with a 'src', 'srcset', 'poster', 'background', 'href', 'data', 'action', 'formaction' or
/// 'style' attribute, such as '&lt;input type=image&gt;', '&lt;video poster&gt;', '&lt;source srcset&gt;', SVG '&lt;use href&gt;'
/// and inline CSS 'url()'; the text between tags stays) and '&lt;style&gt;' blocks, remote reference-style link
/// definitions (a scheme or '//' destination, on the same or the next line, also inside block quotes and list items,
/// with escaped brackets or a label over two lines; their uses keep the text),
/// autolinks, and link targets (the link text stays), repeating until the text is stable, at most
/// <see cref="MaxPasses"/> times. The removal list is the TS sanitizeIngest's; the implementation is its own. Every
/// removal keeps the newlines it covered, so a cleaned document has the same lines as the blob it came from and chunk
/// spans still cite the blob's own lines.
/// </summary>
internal static partial class IngestSanitizer
{
    internal const int MaxPasses = 5;

    /// <summary>
    /// What the sanitizer removes, as a number: part of every stored document's scrub ruleset, so a change here cleans
    /// the stored documents again at the next refresh. Raise it with any change to what is removed.
    /// </summary>
    internal const int Version = 2;

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
        cleaned = StyleBlock.Replace(cleaned, static m => Newlines(m.Value));
        cleaned = HtmlImage.Replace(cleaned, static m => Newlines(m.Value));
        cleaned = HtmlTag.Replace(cleaned, static m => RemoteAttribute.IsMatch(Unquoted(m.Value)) ? Newlines(m.Value) : m.Value);
        cleaned = Image.Replace(cleaned, string.Empty);
        cleaned = ReferenceImage.Replace(cleaned, "${text}");
        cleaned = RemoteReferences(cleaned);
        cleaned = Link.Replace(cleaned, "${text}");
        cleaned = Autolink.Replace(cleaned, string.Empty);
        cleaned = Blob.Replace(cleaned, "[encoded blob removed]");
        return Visible(cleaned);
    }

    /// <summary>
    /// Removes reference definitions whose destination is remote ('[prd]: https://…', '//host/…', a scheme on the next
    /// line, inside '&gt;' or a list item), keeping the lines, and reduces their uses ('[the PRD][prd]') to the link text.
    /// The destination is one token (ending only at an ASCII space; one starting with an entity counts as remote),
    /// followed by the line's end or a title opener (a title may run on), so prose ('[Note]: Warning: …') is kept.
    /// Footnotes ('[^1]: …') and local definitions ('[x]: ./doc.md', 'c:\docs') stay.
    /// </summary>
    private static string RemoteReferences(string text)
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleaned = RemoteDefinition.Replace(text, m =>
        {
            _ = labels.Add(Label(m.Groups["label"].Value));
            return Newlines(m.Value);
        });
        return labels.Count == 0 ? cleaned : ReferenceLink.Replace(cleaned, m =>
            labels.Contains(Label(m.Groups["label"].Value is { Length: > 0 } label ? label : m.Groups["text"].Value)) ? m.Groups["text"].Value + Newlines(m.Value) : m.Value);
    }

    /// <summary>A label as CommonMark matches it: whitespace runs (a line break too) are one space, ends trimmed (case is ignored by the set).</summary>
    private static string Label(string label) => string.Join(' ', label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>A tag with its quoted attribute values blanked, so an attribute name is only matched outside a value.</summary>
    private static string Unquoted(string tag) => QuotedValue.Replace(tag, "\"\"");

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

    [GeneratedRegex("<(?:img|image)\\b(?>(?:\"[^\"]{0,4000}\"|'[^']{0,4000}'|[^'\">]){0,4000})>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlImage { get; }

    /// <summary>A start tag (quoted values may hold '&gt;'), checked by <see cref="RemoteAttribute"/> before it is removed.</summary>
    [GeneratedRegex("<[a-z][a-z0-9:-]{0,40}(?=[ \\t\\r\\n\\f/])(?>(?:\"[^\"]{0,4000}\"|'[^']{0,4000}'|[^'\">]){0,4000})>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTag { get; }

    [GeneratedRegex("[ \\t\\r\\n\\f/\"':](?:src|srcset|lowsrc|dynsrc|poster|background|href|data|action|formaction|style|ping)[ \\t\\r\\n\\f]{0,20}=", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RemoteAttribute { get; }

    [GeneratedRegex("\"[^\"]{0,4000}\"|'[^']{0,4000}'", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuotedValue { get; }

    [GeneratedRegex("<style\\b[^>]{0,4000}>.*?(?:</style[ \\t\\r\\n\\f]{0,20}>|\\z)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StyleBlock { get; }

    [GeneratedRegex("!\\[[^\\]\\n]{0,500}\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Image { get; }

    [GeneratedRegex("^(?>(?:[ ]{0,3}(?:>|[-*+]|[0-9]{1,9}[.)])[ \\t]?){0,10})[ ]{0,3}\\[(?<label>(?!\\^)(?:\\\\[^\\n]|[^\\]\\\\\\n]|\\n(?![ \\t]*\\n)){1,500})\\]:[ \\t]*(?:\\n(?:[ ]{0,3}>)*[ \\t]*)?(?:<(?://|&|[a-z][a-z0-9+.-]{1,31}:)[^>\\n]{0,2000}>|(?://|&|[a-z][a-z0-9+.-]{1,31}:)[^ \\t\\n]{0,2000})(?:[ \\t]*$|[ \\t]+[\"'(][^\\n]{0,2000}$)", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RemoteDefinition { get; }

    [GeneratedRegex("\\[(?<text>[^\\]\\n]{0,500})\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Link { get; }

    [GeneratedRegex("!\\[(?<text>[^\\]\\n]{0,500})\\](?:\\[[^\\]\\n]{0,500}\\])?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceImage { get; }

    [GeneratedRegex("\\[(?<text>[^\\]\\n]{1,500})\\]\\[(?<label>(?:\\\\[^\\n]|[^\\]\\\\\\n]|\\n(?![ \\t]*\\n)){0,500})\\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceLink { get; }

    [GeneratedRegex("<(?:https?|ftp|mailto):[^>\\s]{1,2000}>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Autolink { get; }

    [GeneratedRegex("[A-Za-z0-9+/=_-]{201,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Blob { get; }
}
