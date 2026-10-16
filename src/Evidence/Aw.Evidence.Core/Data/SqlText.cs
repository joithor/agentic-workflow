namespace Aw.Evidence.Core.Data;

/// <summary>Splits SQL text into statements and words without ever looking inside a quoted string, a quoted identifier or a comment.</summary>
public static class SqlText
{
    /// <summary>Splits <paramref name="sql"/> at its top-level semicolons.</summary>
    /// <param name="sql">The SQL text.</param>
    /// <returns>The non-empty statements, comments removed and trimmed, without their semicolons.</returns>
    public static IReadOnlyList<string> Statements(string sql) => throw new NotSupportedException("SqlText.Statements is not written yet");

    /// <summary>The words (letters, digits, underscores) of a statement outside quotes and comments, upper-cased.</summary>
    /// <param name="statement">One statement.</param>
    /// <returns>The words, in order.</returns>
    public static IReadOnlyList<string> Words(string statement) => throw new NotSupportedException("SqlText.Words is not written yet");
}
