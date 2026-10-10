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
    private const string Pairs =
        "АAВBЕEКKМMНHОOРPСCТTУYХXІIЈJЅS"
        + "аaеeоoрpсcуyхxіiјjѕsһhԁdԛqԝwӏl"
        + "ΑAΒBΕEΖZΗHΙIΚKΜMΝNΟOΡPΤTΥYΧX"
        + "αaιiκkνvοoρpυuχxıi";

    private static readonly FrozenDictionary<char, char> _letters = BuildLetters();

    /// <summary>True for characters that render as nothing: zero-width space, joiners, word joiner, BOM, soft hyphen.</summary>
    public static bool IsInvisible(char c) =>
        c is '­' or '᠎' or '﻿' or (>= '​' and <= '‍') or (>= '⁠' and <= '⁤');

    /// <summary>The ASCII character <paramref name="c"/> stands for, or <paramref name="c"/> itself.</summary>
    public static char Fold(char c)
    {
        if (c < '\u0080')
        {
            return c;
        }

        if (c is >= '！' and <= '～')
        {
            return (char)(c - 0xFEE0);
        }

        if (c is ' ' or ' ' or ' ' or ' ' or '　' or (>= ' ' and <= ' '))
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
