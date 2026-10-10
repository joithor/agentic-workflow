using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// Typed parameter binding. Each overload sets the SQLite storage type explicitly, and null binds as SQL NULL,
/// so no value goes through type inference or string formatting (spec §4b.2). To bind NULL with no typed value at
/// hand, cast it ('(string?)null'); a bare <see langword="null"/> is ambiguous between the overloads.
/// </summary>
public static class SqliteBind
{
    /// <summary>Binds a TEXT value, or NULL.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name, for example '$id'.</param>
    /// <param name="value">The value.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, string? value) =>
        Add(command, name, SqliteType.Text, value);

    /// <summary>Binds an INTEGER value, or NULL.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, long? value) =>
        Add(command, name, SqliteType.Integer, value);

    /// <summary>Binds an INTEGER value.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, int value) =>
        Add(command, name, SqliteType.Integer, (long)value);

    /// <summary>
    /// Binds a timestamp as TEXT in UTC round-trip form ('2026-10-10T07:05:07.0420000Z'), the same text AwJson writes.
    /// Fixed width with a Z, so stored timestamps sort and compare as text.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The timestamp, in any offset.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, DateTimeOffset value) =>
        Add(command, name, SqliteType.Text, Timestamp(value));

    /// <summary>
    /// Binds a Guid as lowercase hyphenated TEXT ('0f8fad5b-d9cb-…'): readable in the sqlite3 CLI and the form
    /// SqliteRead.GetGuid accepts. Never a 16-byte BLOB, which is what an untyped parameter would store.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The Guid.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, Guid value) =>
        Add(command, name, SqliteType.Text, value.ToString("D", CultureInfo.InvariantCulture));

    /// <summary>Binds a REAL value, or NULL.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, double? value) =>
        Add(command, name, SqliteType.Real, value);

    /// <summary>Binds a boolean as INTEGER 1 or 0 (SQLite has no boolean type).</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, bool value) =>
        Add(command, name, SqliteType.Integer, value ? 1L : 0L);

    /// <summary>Binds a BLOB value, or NULL.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The bytes.</param>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand Bind(this SqliteCommand command, string name, byte[]? value) =>
        Add(command, name, SqliteType.Blob, value);

    /// <summary>
    /// Binds a strongly typed id stored as TEXT. The accessor reads the id's underlying value, so this works for any
    /// id type (Vogen or a hand-written record struct) without the data layer knowing about it.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="id">The id.</param>
    /// <param name="value">Reads the id's stored text, for example 'id =&gt; id.Value'.</param>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand BindId<TId>(this SqliteCommand command, string name, TId id, Func<TId, string> value)
        where TId : struct, IEquatable<TId>
    {
        ArgumentNullException.ThrowIfNull(value);
        return Add(command, name, SqliteType.Text, value(id));
    }

    /// <summary>Binds a strongly typed id stored as INTEGER.</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="id">The id.</param>
    /// <param name="value">Reads the id's stored integer.</param>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand BindId<TId>(this SqliteCommand command, string name, TId id, Func<TId, long> value)
        where TId : struct, IEquatable<TId>
    {
        ArgumentNullException.ThrowIfNull(value);
        return Add(command, name, SqliteType.Integer, value(id));
    }

    /// <summary>Binds a strongly typed id stored as lowercase hyphenated TEXT (see the Guid overload).</summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="id">The id.</param>
    /// <param name="value">Reads the id's Guid.</param>
    /// <typeparam name="TId">The id type.</typeparam>
    /// <returns>The command, for chaining.</returns>
    public static SqliteCommand BindId<TId>(this SqliteCommand command, string name, TId id, Func<TId, Guid> value)
        where TId : struct, IEquatable<TId>
    {
        ArgumentNullException.ThrowIfNull(value);
        return command.Bind(name, value(id));
    }

    internal static string Timestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static SqliteCommand Add(SqliteCommand command, string name, SqliteType type, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _ = command.Parameters.Add(new SqliteParameter(name, type) { Value = value ?? DBNull.Value });
        return command;
    }
}
