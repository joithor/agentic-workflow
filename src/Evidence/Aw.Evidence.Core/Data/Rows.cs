using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>What a query returned: its column names, at most the row limit of rows, and whether more were left.</summary>
/// <param name="Columns">The column names, in order.</param>
/// <param name="Values">The rows; each holds one value per column (null, bool, a number, string, bytes, a date or time, or an array).</param>
/// <param name="Truncated">True when the row limit cut the result.</param>
/// <param name="SourceColumns">For each column, the name of the table column it reads when the engine can say (null for a computed one); null when the engine can't say at all.</param>
public sealed record Rows(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<object?>> Values, bool Truncated, IReadOnlyList<string?>? SourceColumns = null)
{
    /// <summary>The number of rows kept.</summary>
    public int Count => Values.Count;

    /// <summary>The rows as a JSON array of objects keyed by column name.</summary>
    /// <returns>The JSON text.</returns>
    public string ToJson()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var row in Values)
            {
                writer.WriteStartObject();
                for (var i = 0; i < Columns.Count; i++)
                {
                    writer.WritePropertyName(Columns[i]);
                    WriteValue(writer, i < row.Count ? row[i] : null);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null or DBNull:
                writer.WriteNullValue();
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case byte[] bytes:
                writer.WriteStringValue("\\x" + Convert.ToHexStringLower(bytes));
                break;
            case Array or System.Collections.IEnumerable when value is not string:
                WriteArray(writer, (System.Collections.IEnumerable)value);
                break;
            default:
                WriteScalar(writer, value);
                break;
        }
    }

    private static void WriteArray(Utf8JsonWriter writer, System.Collections.IEnumerable items)
    {
        writer.WriteStartArray();
        foreach (var item in items)
        {
            WriteValue(writer, item);
        }

        writer.WriteEndArray();
    }

    private static void WriteScalar(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case sbyte or byte or short or ushort or int or uint or long or ulong or decimal:
                writer.WriteRawValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null");
                break;
            case float or double:
                WriteFloating(writer, Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            case DateTime moment:
                writer.WriteStringValue(moment.ToString("O", CultureInfo.InvariantCulture));
                break;
            case DateTimeOffset moment:
                writer.WriteStringValue(moment.ToString("O", CultureInfo.InvariantCulture));
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void WriteFloating(Utf8JsonWriter writer, double number)
    {
        if (double.IsFinite(number))
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteStringValue(number.ToString(CultureInfo.InvariantCulture));
        }
    }
}
