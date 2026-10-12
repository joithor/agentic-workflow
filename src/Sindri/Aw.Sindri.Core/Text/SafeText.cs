namespace Aw.Sindri.Core.Text;

/// <summary>The shared sanitizer (API spec §3.6): text a view stores is scrubbed, then escaped so a terminal or the UI shows it as written, then cut to one line.</summary>
public static class SafeText
{
    /// <summary>Makes every control visible: C0 and C1 controls become '\xNN', invisible and bidi marks '\u{XXXX}', and an ESC-introduced CSI or OSC sequence is escaped whole. Nothing is dropped silently.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text)
        => throw new NotSupportedException("SafeText.Escape: P3 unit A makes Escape_turns_controls_into_visible_text pass");

    /// <summary>One line: newlines become spaces, the ends are trimmed, and text longer than <paramref name="max"/> is cut with '…'.</summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The most characters to keep.</param>
    /// <returns>The line.</returns>
    public static string Line(string text, int max)
        => throw new NotSupportedException("SafeText.Line: P3 unit A makes Line_folds_newlines_and_cuts_at_max pass");

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
}
