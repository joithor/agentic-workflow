using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Infrastructure.Scrubbing;

/// <summary>
/// The first half of the ingest boundary (spec §7): removes HTML comments, invisible and control characters (tab and
/// newline stay), encoded blobs over 200 characters, images (markdown inline and reference-style, wherever the
/// definition is, also when their text runs across line breaks (not a blank line), and HTML '&lt;img&gt;' and '&lt;image&gt;' tags), the elements
/// that can fetch, whenever they have an attribute
/// ('&lt;meta&gt;', '&lt;iframe&gt;', '&lt;frame&gt;', '&lt;object&gt;', '&lt;embed&gt;', '&lt;link&gt;', '&lt;base&gt;', '&lt;script&gt;', SVG
/// '&lt;set&gt;' and '&lt;animate…&gt;'), every other HTML tag that can fetch or point at a remote resource (one with a 'src',
/// 'srcset', 'imagesrcset', 'srcdoc', 'poster', 'background', 'href', 'data', 'action', 'formaction', 'style' or 'ping'
/// attribute anywhere in the tag, such as '&lt;input type=image&gt;', '&lt;video poster&gt;', '&lt;source srcset&gt;', SVG
/// '&lt;use href&gt;' and inline CSS 'url()'; the text between tags stays; a tag whose quotes don't balance is read up to
/// its first '&gt;' or a blank line, so it is removed rather than kept) and '&lt;style&gt;' blocks, remote reference-style link
/// definitions (a scheme or '//' destination, on the same or the next line, also inside block quotes and list items,
/// with escaped brackets or a label over two lines; their uses keep the text),
/// autolinks, and link targets (the link text stays, also across line breaks), repeating until the text
/// is stable; a text still changing after <see cref="MaxPasses"/> passes is refused (null; the caller blocks it). The removal list is the TS sanitizeIngest's; the implementation is its own. Every
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
    internal const int Version = 5;

    /// <summary>The clean text, or null when it still changes after <see cref="MaxPasses"/> passes (the caller blocks it).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The clean text, or null.</returns>
    /// <exception cref="RegexMatchTimeoutException">A rule ran past its timeout (the caller blocks the text too).</exception>
    internal static string? Clean(string text)
    {
        for (var pass = 0; pass <= MaxPasses; pass++)
        {
            var next = Once(text);
            if (string.Equals(next, text, StringComparison.Ordinal))
            {
                return next;
            }

            text = next;   // the last round only confirms: a text the fifth pass settled is kept
        }

        // Still changing: a later removal may have formed something an earlier rule would remove (a tag joined across
        // a peeled link), so the text is never kept half-cleaned.
        return null;
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
        cleaned = RawFetchingTag.Replace(cleaned, static m => Newlines(m.Value));   // what the quote-aware pass couldn't parse (an unbalanced quote, a long value)
        cleaned = HtmlTag.Replace(cleaned, static m => RemoteAttribute.IsMatch(m.Value) ? Newlines(m.Value) : m.Value);
        cleaned = RawTag.Replace(cleaned, static m => RemoteAttribute.IsMatch(m.Value) ? Newlines(m.Value) : m.Value);
        cleaned = Image.Replace(cleaned, static m => Newlines(m.Value));
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

    /// <summary>
    /// Every rule the sanitizer applies, as text: part of the scrub ruleset, so a changed regex cleans the stored
    /// documents again even if <see cref="Version"/> wasn't raised.
    /// </summary>
    internal static string Rules { get; } = string.Join('\n', ((Regex[])[Comment, StyleBlock, HtmlImage, RawFetchingTag, HtmlTag, RawTag, RemoteAttribute, Image,
        ReferenceImage, RemoteDefinition, ReferenceLink, Link, Autolink, Blob]).Select(r => $"{r.Options}:{r}"));

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

    /// <summary>An element that fetches or points at something remote whatever its attributes, quote-aware (a quoted value may hold '&gt;').</summary>
    [GeneratedRegex("<(?:img|image)\\b(?>(?:\"[^\"]{0,4000}\"|'[^']{0,4000}'|[^'\">\\n]|\\n(?![ \\t]*\\n)){0,4000})>|<(?:img|image|meta|iframe|frame|object|embed|link|base|script|set|animate[a-z]{0,20})(?=[ \\t\\r\\n\\f/])(?>(?:\"[^\"]{0,4000}\"|'[^']{0,4000}'|[^'\">\\n]|\\n(?![ \\t]*\\n)){0,4000})>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlImage { get; }

    /// <summary>The same elements read raw, up to the first '&gt;' (or the end): fails closed where the quote-aware read can't parse.</summary>
    [GeneratedRegex("<(?:(?:img|image)\\b|(?:img|image|meta|iframe|frame|object|embed|link|base|script|set|animate[a-z]{0,20})(?=[ \\t\\r\\n\\f/]))(?:[^>\\n]|\\n(?![ \\t]*\\n)){0,8000}(?:>|\\z|(?=\\n[ \\t]*\\n))", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RawFetchingTag { get; }

    /// <summary>A start tag (quoted values may hold '&gt;'), checked by <see cref="RemoteAttribute"/> before it is removed.</summary>
    [GeneratedRegex("<[a-z][a-z0-9:-]{0,40}(?=[ \\t\\r\\n\\f/])(?>(?:\"[^\"]{0,4000}\"|'[^']{0,4000}'|[^'\">\\n]|\\n(?![ \\t]*\\n)){0,4000})>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTag { get; }

    /// <summary>A start tag read raw, up to the first '&gt;' (or the end), for the tags the quote-aware read can't parse.</summary>
    [GeneratedRegex("<[a-z][a-z0-9:-]{0,40}(?=[ \\t\\r\\n\\f/])(?:[^>\\n]|\\n(?![ \\t]*\\n)){0,8000}(?:>|\\z|(?=\\n[ \\t]*\\n))", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RawTag { get; }

    [GeneratedRegex("[ \\t\\r\\n\\f/\"':](?:src|srcset|imagesrcset|srcdoc|lowsrc|dynsrc|poster|background|href|data|action|formaction|style|ping)[ \\t\\r\\n\\f]{0,4000}=", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RemoteAttribute { get; }

    [GeneratedRegex("<style\\b[^>]{0,4000}>.*?(?:</style[ \\t\\r\\n\\f]{0,20}>|\\z)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StyleBlock { get; }

    [GeneratedRegex("!\\[(?:[^\\]\\n]|\\n(?![ \\t]*\\n)){0,500}\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Image { get; }

    [GeneratedRegex("^(?>(?:[ ]{0,3}(?:>|[-*+]|[0-9]{1,9}[.)])[ \\t]?){0,10})[ ]{0,3}\\[(?<label>(?!\\^)(?:\\\\[^\\n]|[^\\]\\\\\\n]|\\n(?![ \\t]*\\n)){1,500})\\]:[ \\t]*(?:\\n(?:[ ]{0,3}>)*[ \\t]*)?(?:<(?://|&|[a-z][a-z0-9+.-]{1,31}:)[^>\\n]{0,2000}>|(?://|&|[a-z][a-z0-9+.-]{1,31}:)[^ \\t\\n]{0,2000})(?:[ \\t]*$|[ \\t]+[\"'(][^\\n]{0,2000}$)", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RemoteDefinition { get; }

    [GeneratedRegex("\\[(?<text>(?:[^\\]\\n]|\\n(?![ \\t]*\\n)){0,500})\\]\\([^)\\n]{0,2000}\\)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Link { get; }

    [GeneratedRegex("!\\[(?<text>(?:[^\\]\\n]|\\n(?![ \\t]*\\n)){0,500})\\](?:\\[[^\\]\\n]{0,500}\\])?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceImage { get; }

    [GeneratedRegex("\\[(?<text>[^\\]\\n]{1,500})\\]\\[(?<label>(?:\\\\[^\\n]|[^\\]\\\\\\n]|\\n(?![ \\t]*\\n)){0,500})\\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReferenceLink { get; }

    [GeneratedRegex("<(?:https?|ftp|mailto):[^>\\s]{1,2000}>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Autolink { get; }

    [GeneratedRegex("[A-Za-z0-9+/=_-]{201,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Blob { get; }
}
