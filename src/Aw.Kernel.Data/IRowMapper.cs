using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Maps the current row of a reader to one value. One mapper per row type; columns are read by ordinal (spec §4b.2).</summary>
/// <typeparam name="T">The row type.</typeparam>
public interface IRowMapper<out T>
{
    /// <summary>Maps the row the reader is on.</summary>
    /// <param name="reader">A reader positioned on a row.</param>
    /// <returns>The mapped value.</returns>
    public T Map(SqliteDataReader reader);
}
