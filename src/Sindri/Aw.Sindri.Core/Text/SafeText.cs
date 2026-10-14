using System.Globalization;
using System.Text;

namespace Aw.Sindri.Core.Text;

/// <summary>The shared sanitizer (API spec \u00A73.6): text a view stores is scrubbed, then escaped so a terminal or the UI shows it as written, then cut to one line.</summary>
public static class SafeText
{
    private const char Ellipsis = '…';

    /// <summary>
    /// Makes every control visible: C0 and C1 controls and DEL become '\xNN'; format characters (zero-width and bidi marks,
    /// tags), line and paragraph separators and lone surrogates become '\u{XXXX}'. An ESC-introduced CSI or OSC sequence is
    /// escaped whole, because its ESC, BEL and C1 bytes are, and its printable bytes then show as text. Tab, line feed and
    /// carriage return stay for <see cref="Line"/> to fold. Nothing is dropped silently.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder? escaped = null;
        var at = 0;
        while (at < text.Length)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(at), out var rune, out var consumed);
            var mark = status == System.Buffers.OperationStatus.Done ? Mark(rune) : Hex("\\u{", text[at], "X4", "}");
            if (mark is not null)
            {
                escaped ??= new StringBuilder(text.Length + 16).Append(text, 0, at);
                _ = escaped.Append(mark);
            }
            else
            {
                _ = escaped?.Append(text, at, consumed);
            }

            at += status == System.Buffers.OperationStatus.Done ? consumed : 1;
        }

        return escaped?.ToString() ?? text;
    }

    /// <summary>One line: each run of tabs and line breaks becomes one space, the ends are trimmed, and text longer than <paramref name="max"/> is cut with '…' (never inside a surrogate pair).</summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The most characters to keep, the '…' included; at least 1.</param>
    /// <returns>The line.</returns>
    public static string Line(string text, int max)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        var line = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is not ('\t' or '\n' or '\r'))
            {
                _ = line.Append(c);
            }
            else if (line.Length > 0 && line[^1] != ' ')
            {
                _ = line.Append(' ');
            }
        }

        var folded = line.ToString().Trim();
        if (folded.Length <= max)
        {
            return folded;
        }

        var keep = max - 1;
        if (keep > 0 && char.IsHighSurrogate(folded[keep - 1]))
        {
            keep--;
        }

        return folded[..keep] + Ellipsis;
    }

    /// <summary>Scrubs, escapes and folds <paramref name="text"/> into one line of at most <paramref name="max"/> characters.</summary>
    /// <param name="scrubber">The secret scrubber.</param>
    /// <param name="text">The text.</param>
    /// <param name="max">The most characters to keep.</param>
    /// <returns>The clean line.</returns>
    public static string Clean(ITextScrubber scrubber, string text, int max)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        return Line(Escape(scrubber.Scrub(text)), max);
    }

    private static string? Mark(Rune rune)
    {
        var value = rune.Value;
        if (value is '\t' or '\n' or '\r')
        {
            return null;
        }

        if (value is < 0x20 or (>= 0x7F and <= 0x9F))
        {
            return Hex("\\x", value, "X2", string.Empty);
        }

        return Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            ? Hex("\\u{", value, "X4", "}")
            : null;
    }

    private static string Hex(string open, int value, string format, string close) =>
        open + value.ToString(format, CultureInfo.InvariantCulture) + close;
}
