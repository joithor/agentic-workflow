using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Reads the values <see cref="SqliteBind"/> writes, strictly, for use in row mappers.</summary>
public static class SqliteRead
{
    /// <summary>
    /// Reads a Guid stored as lowercase hyphenated TEXT (what <see cref="SqliteBind"/> and Node's crypto.randomUUID()
    /// write). Anything else (a BLOB, upper case, braces, junk) is a data bug and throws, so a row never round-trips
    /// to different bytes.
    /// </summary>
    /// <param name="reader">A reader on a row.</param>
    /// <param name="ordinal">The column.</param>
    /// <returns>The Guid.</returns>
    /// <exception cref="InvalidDataException">The column isn't a lowercase hyphenated Guid.</exception>
    public static Guid GetGuid(SqliteDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var value = reader.GetValue(ordinal);
        if (value is string text
            && Guid.TryParseExact(text, "D", out var guid)
            && string.Equals(text, guid.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return guid;
        }

        throw new InvalidDataException(
            $"Column {reader.GetName(ordinal)} holds {value.GetType().Name} '{value}', not a lowercase hyphenated Guid.");
    }
}
