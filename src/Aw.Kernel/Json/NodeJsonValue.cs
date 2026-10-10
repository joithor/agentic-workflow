using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aw.Kernel.Json;

/// <summary>
/// 'JSON.stringify(JSON.parse(text))' for untyped JSON: numbers become doubles and are re-laid out like
/// Number#toString, array-index keys ("0" to "4294967294", no leading zeros) move first in ascending order, a
/// duplicate key keeps its first position and its last value, and strings are re-escaped the Node way (a lone
/// surrogate stays an escape).
/// </summary>
internal static class NodeJsonValue
{
    private const int MaxDepth = 1000;
    private static readonly object _jsonNull = new();

    public static Result<string> Restringify(ReadOnlySpan<byte> utf8Json)
    {
        var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions { MaxDepth = MaxDepth });
        try
        {
            _ = reader.Read();
            var value = ReadValue(ref reader);
            _ = reader.Read();
            var builder = new StringBuilder(utf8Json.Length);
            Write(builder, value);
            return builder.ToString();
        }
        catch (JsonException ex)
        {
            return KernelErrors.Invalid.With("Not JSON that JSON.parse accepts: " + ex.Message);
        }
    }

    /// <summary>Writes the re-stringified form of <paramref name="utf8Json"/> as a raw value; the input is already valid JSON.</summary>
    public static void WriteRestringified(Utf8JsonWriter writer, ReadOnlySpan<byte> utf8Json)
    {
        var text = Restringify(utf8Json);
        writer.WriteRawValue(text.IsSuccess ? text.Value : throw new JsonException(text.Error.Message), skipInputValidation: true);
    }

    private static object ReadValue(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.StartObject => ReadObject(ref reader),
        JsonTokenType.StartArray => ReadArray(ref reader),
        JsonTokenType.String => ReadText(ref reader),
        JsonTokenType.Number => double.Parse(reader.ValueSpan, NumberStyles.Float, CultureInfo.InvariantCulture),
        JsonTokenType.True => true,
        JsonTokenType.False => false,
        JsonTokenType.Null => _jsonNull,
        JsonTokenType.None or JsonTokenType.EndObject or JsonTokenType.EndArray or JsonTokenType.PropertyName
            or JsonTokenType.Comment => throw new JsonException($"Unexpected {reader.TokenType}."),
        _ => throw new JsonException($"Unexpected {reader.TokenType}."),
    };

    private static JsObject ReadObject(ref Utf8JsonReader reader)
    {
        var obj = new JsObject();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = ReadText(ref reader);
            _ = reader.Read();
            obj.Set(key, ReadValue(ref reader));
        }

        return obj;
    }

    private static List<object> ReadArray(ref Utf8JsonReader reader)
    {
        var items = new List<object>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            items.Add(ReadValue(ref reader));
        }

        return items;
    }

    // The reader's own unescaping rejects lone surrogates, which JSON.parse keeps; unescape the raw token instead.
    private static string ReadText(ref Utf8JsonReader reader)
    {
        var raw = reader.HasValueSequence ? Encoding.UTF8.GetString(reader.ValueSequence) : Encoding.UTF8.GetString(reader.ValueSpan);
        return reader.ValueIsEscaped ? NodeJsonEscaping.Unescape(raw) : raw;
    }

    private static void Write(StringBuilder builder, object value)
    {
        switch (value)
        {
            case string text:
                _ = builder.Append(NodeJsonEscaping.Quote(text));
                break;
            case double number:
                _ = builder.Append(JsNumber.Format(number));
                break;
            case bool flag:
                _ = builder.Append(flag ? "true" : "false");
                break;
            case List<object> items:
                WriteArray(builder, items);
                break;
            case JsObject obj:
                WriteObject(builder, obj);
                break;
            default:
                _ = builder.Append("null");
                break;
        }
    }

    private static void WriteArray(StringBuilder builder, List<object> items)
    {
        _ = builder.Append('[');
        for (var i = 0; i < items.Count; i++)
        {
            _ = i > 0 ? builder.Append(',') : builder;
            Write(builder, items[i]);
        }

        _ = builder.Append(']');
    }

    private static void WriteObject(StringBuilder builder, JsObject obj)
    {
        _ = builder.Append('{');
        var first = true;
        foreach (var (key, value) in obj.InJsOrder())
        {
            _ = first ? builder : builder.Append(',');
            first = false;
            _ = builder.Append(NodeJsonEscaping.Quote(key)).Append(':');
            Write(builder, value);
        }

        _ = builder.Append('}');
    }

    /// <summary>A parsed JSON object with the property order JS gives it.</summary>
    private sealed class JsObject
    {
        private readonly List<string> _order = [];
        private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

        public void Set(string key, object value)
        {
            if (_values.TryAdd(key, value))
            {
                _order.Add(key);
            }
            else
            {
                _values[key] = value;
            }
        }

        public IEnumerable<(string Key, object Value)> InJsOrder() =>
            _order.Where(IsArrayIndex).OrderBy(ArrayIndex).Concat(_order.Where(k => !IsArrayIndex(k)))
                .Select(k => (k, _values[k]));

        private static bool IsArrayIndex(string key) =>
            key.Length is > 0 and <= 10 && (key.Length == 1 || key[0] != '0') && key.All(char.IsAsciiDigit)
            && ArrayIndex(key) != uint.MaxValue;

        private static uint ArrayIndex(string key) =>
            uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? index : uint.MaxValue;
    }
}
