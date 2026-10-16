using Aw.Kernel;

namespace Aw.Evidence.Core.Data;

/// <summary>
/// The pre-run refusal of a statement that writes: a clear early error before anything connects. It is not the safeguard (the
/// connection is); a function that writes and nextval pass it on purpose.
/// </summary>
public static class WriteStatements
{
    private static readonly string[] _readStarts = ["SELECT", "WITH", "VALUES", "TABLE", "SHOW", "EXPLAIN", "PRAGMA"];

    // A CTE can carry a write, SELECT … INTO creates a table, and EXPLAIN ANALYZE runs what it explains.
    private static readonly string[] _writeWords = ["INSERT", "UPDATE", "DELETE", "MERGE", "INTO"];

    private static readonly string[] _readPragmas =
        ["TABLE_INFO", "TABLE_XINFO", "INDEX_LIST", "INDEX_INFO", "INDEX_XINFO", "FOREIGN_KEY_LIST", "DATABASE_LIST", "TABLE_LIST"];

    /// <summary>Checks one query's text.</summary>
    /// <param name="sql">The SQL text of one query.</param>
    /// <returns>Success, or AW-EVIDENCE-020 naming the statement kind that is refused.</returns>
    public static Result<Unit> Check(string sql)
    {
        var statements = SqlText.Statements(sql);
        if (statements.Count == 0)
        {
            return EvidenceErrors.DatabaseRefused.With("the query holds no statement");
        }

        foreach (var statement in statements)
        {
            var refused = Refusal(statement);
            if (refused is not null)
            {
                return EvidenceErrors.DatabaseRefused.With(refused);
            }
        }

        return Unit.Value;
    }

    private static string? Refusal(string statement)
    {
        var words = SqlText.Words(statement);
        var first = words.Count == 0 ? string.Empty : words[0];
        if (!_readStarts.Contains(first, StringComparer.Ordinal))
        {
            return $"{first} is not a read statement: db-run only runs SELECT, WITH, VALUES, TABLE, SHOW, EXPLAIN and read PRAGMAs";
        }

        if (words.FirstOrDefault(word => _writeWords.Contains(word, StringComparer.Ordinal)) is { } write)
        {
            return $"{write} in a query: db-run reads, it never writes (quote the identifier if {write} is a column name)";
        }

        return first == "PRAGMA" && !IsReadPragma(statement, words) ? "PRAGMA with an assignment or one that is not a read is refused" : null;
    }

    private static bool IsReadPragma(string statement, IReadOnlyList<string> words) =>
        words.Count > 1 && _readPragmas.Contains(words[1], StringComparer.Ordinal) && !SqlText.Masked(statement).Contains('=', StringComparison.Ordinal);
}
