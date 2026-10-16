using System.Text.RegularExpressions;
using Npgsql;

namespace Aw.Evidence.Infrastructure.Databases;

/// <summary>A Postgres error as text that is safe to record: the SQLSTATE and the message, without the values the server quotes in it.</summary>
internal static partial class ServerMessage
{
    private const string Placeholder = "\"…\"";

    /// <summary>The SQLSTATE and the message text. Outside class 42 (syntax errors and missing names, which quote the script's own identifiers) every double-quoted literal becomes a placeholder: a data error such as invalid input syntax for type uuid quotes the offending value, which may be a secret.</summary>
    /// <param name="exception">The server's error.</param>
    /// <returns>The text.</returns>
    internal static string Of(PostgresException exception)
    {
        var text = exception.SqlState.StartsWith("42", StringComparison.Ordinal) ? exception.MessageText : Quoted.Replace(exception.MessageText, Placeholder);
        return exception.SqlState + " " + text;
    }

    [GeneratedRegex("\"(?:[^\"]|\"\")*\"", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex Quoted { get; }
}
