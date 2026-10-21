namespace Aw.CodeIndex.Core.Similarity;

/// <summary>The lexical tokenizer the clones layer reads (spec §5.3): any C-like or scripting language, no parse. Comments are dropped and literals blanked.</summary>
public static class CloneTokenizer
{
    /// <summary>Splits source text into tokens: identifiers and keywords as written, one token per punctuation character, '0' for a number and '"' for a string.</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The tokens in order.</returns>
    public static IReadOnlyList<string> Tokenize(string text) => throw new NotSupportedException();

    /// <summary>Tokenizes only the lines of a symbol's span.</summary>
    /// <param name="text">The whole file's text.</param>
    /// <param name="startLine">The first line, 1-based.</param>
    /// <param name="endLine">The last line, inclusive.</param>
    /// <returns>The tokens of those lines; empty when the span lies outside the text.</returns>
    public static IReadOnlyList<string> TokenizeLines(string text, int startLine, int endLine) => throw new NotSupportedException();
}
