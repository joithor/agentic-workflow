using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>An <see cref="IRowMapper{T}"/> over a function, for the stores' one-off row shapes.</summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="map">Reads the current row by ordinal.</param>
internal sealed class RowMap<T>(Func<SqliteDataReader, T> map) : IRowMapper<T>
{
    public T Map(SqliteDataReader reader) => map(reader);
}
