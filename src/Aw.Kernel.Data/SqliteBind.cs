using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>
/// Typed parameter binding. Each overload sets the SQLite storage type explicitly, and null binds as SQL NULL,
/// so no value goes through type inference or string formatting (spec §4b.2).
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

    private static SqliteCommand Add(SqliteCommand command, string name, SqliteType type, object? value)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _ = command.Parameters.Add(new SqliteParameter(name, type) { Value = value ?? DBNull.Value });
        return command;
    }
}
