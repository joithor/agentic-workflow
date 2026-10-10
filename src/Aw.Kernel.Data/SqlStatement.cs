using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>One SQL statement, how to bind its parameters, and what it is about (for error messages).</summary>
/// <param name="Sql">The SQL. Keep it in a const next to the repository, or in an embedded .sql file (spec §4b.2).</param>
/// <param name="Bind">Binds the parameters, or null when there are none.</param>
/// <param name="Subject">What the statement reads or writes, for example 'item 42'. Defaults to the SQL.</param>
public sealed record SqlStatement(string Sql, Action<SqliteCommand>? Bind = null, string? Subject = null)
{
    internal string Describe() => Subject ?? Sql;
}
