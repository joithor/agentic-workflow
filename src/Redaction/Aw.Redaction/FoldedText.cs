using System.Globalization;
using System.Text;

namespace Aw.Redaction;

/// <summary>
/// The text the rules scan: the input with its characters folded (<see cref="Homoglyphs"/>) and its JSON escapes
/// decoded (a backslash-n in serialized JSON is a newline, so a token right after it still starts a word). Each
/// folded character remembers the span of the input it came from, so a hit maps back to the original text.
/// </summary>
internal sealed class FoldedText
{
    private readonly int[] _starts;
    private readonly int[] _ends;

    private FoldedText(string text, int[] starts, int[] ends)
    {
        Text = text;
        _starts = starts;
        _ends = ends;
    }

    /// <summary>The folded text.</summary>
    public string Text { get; }

    /// <summary>Folds <paramref name="original"/>.</summary>
    public static FoldedText Fold(string original)
    {
        var text = new StringBuilder(original.Length);
        var starts = new List<int>(original.Length);
        var ends = new List<int>(original.Length);
        var i = 0;
        while (i < original.Length)
        {
            var (c, width) = Decode(original, i);
            if (!Homoglyphs.IsInvisible(c))
            {
                _ = text.Append(Homoglyphs.Fold(c));
                starts.Add(i);
                ends.Add(i + width);
            }

            i += width;
        }

        return new FoldedText(text.ToString(), [.. starts], [.. ends]);
    }

    /// <summary>Maps the folded span [<paramref name="start"/>, <paramref name="end"/>) to the original text.</summary>
    public (int Start, int End) ToOriginal(int start, int end) => (_starts[start], _ends[end - 1]);

    // One character of the input: a JSON escape (\n \r \t \" \/ \\ \uXXXX) decodes to the character it stands for.
    private static (char Char, int Width) Decode(string s, int i)
    {
        if (s[i] != '\\' || i + 1 >= s.Length)
        {
            return (s[i], 1);
        }

        return s[i + 1] switch
        {
            'n' => ('\n', 2),
            'r' => ('\r', 2),
            't' => ('\t', 2),
            '"' or '/' or '\\' => (s[i + 1], 2),
            'u' when TryHex(s, i + 2, out var code) => ((char)code, 6),
            _ => (s[i], 1),
        };
    }

    private static bool TryHex(string s, int at, out int code)
    {
        code = 0;
        return at + 4 <= s.Length
            && int.TryParse(s.AsSpan(at, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code);
    }
}
