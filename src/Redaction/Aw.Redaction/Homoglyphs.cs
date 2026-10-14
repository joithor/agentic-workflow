using System.Collections.Frozen;

namespace Aw.Redaction;

/// <summary>
/// The character folding the scan runs on (never the output): invisible characters are dropped, Unicode spaces become
/// a space, fullwidth ASCII and the Cyrillic and Greek letters that look like Latin ones become ASCII. So a token
/// written with a look-alike prefix or a zero-width joiner inside still matches its rule (spec §4c).
/// </summary>
internal static class Homoglyphs
{
    // Pairs: look-alike, then the ASCII letter it folds to. Cyrillic, then Greek, then Latin extras.
    internal const string Pairs =
        "\u0410A\u0412B\u0415E\u041AK\u041CM\u041DH\u041EO\u0420P\u0421C\u0422T\u0423Y\u0425X\u0406I\u0408J\u0405S"
        + "\u0430a\u0435e\u043Eo\u0440p\u0441c\u0443y\u0445x\u0456i\u0458j\u0455s\u04BBh\u0501d\u051Bq\u051Dw\u04CFl"
        + "\u0391A\u0392B\u0395E\u0396Z\u0397H\u0399I\u039AK\u039CM\u039DN\u039FO\u03A1P\u03A4T\u03A5Y\u03A7X"
        + "\u03B1a\u03B9i\u03BAk\u03BDv\u03BFo\u03C1p\u03C5u\u03C7x\u0131i\u0261g";

    private static readonly FrozenDictionary<char, char> _letters = BuildLetters();

    /// <summary>True for characters that render as nothing: zero-width space, joiners, word joiner, BOM, soft hyphen.</summary>
    public static bool IsInvisible(char c) =>
        c is '\u00AD' or '\u180E' or '\uFEFF' or (>= '\u200B' and <= '\u200D') or (>= '\u2060' and <= '\u2064');

    /// <summary>The ASCII character <paramref name="c"/> stands for, or <paramref name="c"/> itself.</summary>
    public static char Fold(char c)
    {
        if (c < '\u0080')
        {
            return c;
        }

        if (c is >= '\uFF01' and <= '\uFF5E')
        {
            return (char)(c - 0xFEE0);
        }

        if (c is '\u00A0' or '\u1680' or '\u202F' or '\u205F' or '\u3000' or '\u2028' or '\u2029' or (>= '\u2000' and <= '\u200A'))
        {
            return ' ';
        }

        return _letters.TryGetValue(c, out var ascii) ? ascii : c;
    }

    private static FrozenDictionary<char, char> BuildLetters()
    {
        var map = new Dictionary<char, char>(Pairs.Length / 2);
        for (var i = 0; i < Pairs.Length; i += 2)
        {
            map[Pairs[i]] = Pairs[i + 1];
        }

        return map.ToFrozenDictionary();
    }
}
