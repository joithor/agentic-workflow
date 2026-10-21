namespace Aw.CodeIndex.Core.Similarity;

/// <summary>The lexical tokenizer the clones layer reads (spec §5.3): any C-like or scripting language, no parse. Comments are dropped and literals blanked.</summary>
public static class CloneTokenizer
{
    private const string StringToken = "\"";
    private const string NumberToken = "0";

    /// <summary>Splits source text into tokens: identifiers and keywords as written, one token per punctuation character, '0' for a number and '"' for a string.</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The tokens in order.</returns>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '#' || (c == '/' && Next(text, i) == '/'))
            {
                i = EndOfLine(text, i);
            }
            else if (c == '/' && Next(text, i) == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? text.Length : end + 2;
            }
            else if (c is '"' or '\'' or '`')
            {
                i = EndOfString(text, i, c);
                tokens.Add(StringToken);
            }
            else if (char.IsDigit(c))
            {
                i = EndOfNumber(text, i);
                tokens.Add(NumberToken);
            }
            else if (IsWordStart(c))
            {
                var end = i + 1;
                while (end < text.Length && (IsWordStart(text[end]) || char.IsDigit(text[end])))
                {
                    end++;
                }

                tokens.Add(text[i..end]);
                i = end;
            }
            else
            {
                tokens.Add(c.ToString());
                i++;
            }
        }

        return tokens;
    }

    /// <summary>Tokenizes only the lines of a symbol's span.</summary>
    /// <param name="text">The whole file's text.</param>
    /// <param name="startLine">The first line, 1-based.</param>
    /// <param name="endLine">The last line, inclusive.</param>
    /// <returns>The tokens of those lines; empty when the span lies outside the text.</returns>
    public static IReadOnlyList<string> TokenizeLines(string text, int startLine, int endLine)
    {
        if (startLine < 1 || endLine < startLine)
        {
            return [];
        }

        var start = 0;
        for (var line = 1; line < startLine; line++)
        {
            var newline = text.IndexOf('\n', start);
            if (newline < 0)
            {
                return [];
            }

            start = newline + 1;
        }

        var end = start;
        for (var line = startLine; line <= endLine && end < text.Length; line++)
        {
            end = EndOfLine(text, end);
            if (line < endLine && end < text.Length)
            {
                end++;
            }
        }

        return Tokenize(text[start..end]);
    }

    private static char Next(string text, int i) => i + 1 < text.Length ? text[i + 1] : '\0';

    private static bool IsWordStart(char c) => char.IsLetter(c) || c is '_' or '$';

    private static int EndOfLine(string text, int from)
    {
        var newline = text.IndexOf('\n', from);
        return newline < 0 ? text.Length : newline;
    }

    // A quote or apostrophe string ends at its closing quote or at the end of its line; a backtick string may span lines.
    private static int EndOfString(string text, int from, char quote)
    {
        var i = from + 1;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == quote)
            {
                return i + 1;
            }

            if (c == '\n' && quote != '`')
            {
                return i;
            }

            i += c == '\\' ? 2 : 1;
        }

        return text.Length;
    }

    private static int EndOfNumber(string text, int from)
    {
        var i = from + 1;
        while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_' || (text[i] == '.' && char.IsDigit(Next(text, i)))))
        {
            i++;
        }

        return i;
    }
}
