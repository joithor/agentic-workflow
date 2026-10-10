using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data;

/// <summary>Reads the values <see cref="SqliteBind"/> writes, strictly, for use in row mappers.</summary>
public static class SqliteRead
{
    /// <summary>
    /// Reads a Guid stored as lowercase hyphenated TEXT (what <see cref="SqliteBind"/> writes). Anything else (a BLOB, upper case, braces, junk) is a data bug and throws, so a row never round-trips
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

    /// <summary>
    /// Reads a timestamp stored as UTC round-trip TEXT ('2026-10-10T07:05:07.0420000Z', what <see cref="SqliteBind"/>
    /// writes) as a <see cref="DateTimeOffset"/> at offset zero. Any other text is a data bug and throws.
    /// </summary>
    /// <param name="reader">A reader on a row.</param>
    /// <param name="ordinal">The column.</param>
    /// <returns>The timestamp.</returns>
    /// <exception cref="InvalidDataException">The column isn't UTC round-trip text.</exception>
    public static DateTimeOffset GetTimestamp(SqliteDataReader reader, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var value = reader.GetValue(ordinal);
        if (value is string text
            && DateTimeOffset.TryParseExact(text, "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
        {
            return timestamp;
        }

        throw new InvalidDataException(
            $"Column {reader.GetName(ordinal)} holds {value.GetType().Name} '{value}', not a UTC round-trip timestamp.");
    }
}
