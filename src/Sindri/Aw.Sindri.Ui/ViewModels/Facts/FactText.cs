using System.Globalization;
using System.Text;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>Text a fact serves is shown scrubbed: a control, bidi or other invisible character is replaced, so a body can't draw over the panel or reorder what is read.</summary>
internal static class FactText
{
    private const char Replacement = '�';

    /// <summary>Replaces every character that draws nothing or changes how text is laid out (a line break and a tab stay).</summary>
    /// <param name="text">The served text.</param>
    /// <returns>The text, safe to show.</returns>
    internal static string Scrub(string text)
    {
        if (!text.EnumerateRunes().Any(Hidden))
        {
            return text;
        }

        var shown = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            _ = Hidden(rune) ? shown.Append(Replacement) : shown.Append(rune.ToString());
        }

        return shown.ToString();
    }

    private static bool Hidden(Rune rune) => rune.Value is not ('\n' or '\t') && Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
        or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned;
}
