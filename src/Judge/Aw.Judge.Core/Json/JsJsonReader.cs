using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aw.Judge.Core.Json;

/// <summary>JSON.parse over Utf8JsonReader: the same grammar (no comments, no trailing commas, one value), strings
/// unescaped by hand so a lone-surrogate escape survives, numbers as doubles (1e400 is Infinity).</summary>
internal static class JsJsonReader
{
    // JSON.parse has no small nesting limit; the reader's default of 64 would refuse what node reads.
    private static readonly JsonReaderOptions _options = new() { MaxDepth = 100_000 };

    internal static JsValue? Read(string text)
    {
        if (text.StartsWith('﻿'))
        {
            return null;   // JSON.parse doesn't treat a byte-order mark as whitespace
        }

        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text), _options);
        try
        {
            return ReadDocument(ref reader);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsValue? ReadDocument(ref Utf8JsonReader reader)
    {
        var parents = new Stack<JsValue>();
        var key = "";
        JsValue? root = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                key = Text(ref reader);
                continue;
            }

            if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                _ = parents.Pop();
                continue;
            }

            var value = Value(ref reader);
            Attach(parents, key, value, ref root);
            if (value is JsObject or JsArray)
            {
                parents.Push(value);
            }
        }

        return root;
    }

    private static void Attach(Stack<JsValue> parents, string key, JsValue value, ref JsValue? root)
    {
        switch (parents.Count == 0 ? null : parents.Peek())
        {
            case JsObject parent:
                _ = parent.Set(key, value);
                break;
            case JsArray parent:
                parent.Add(value);
                break;
            default:
                root = value;
                break;
        }
    }

    private static JsValue Value(ref Utf8JsonReader reader) => reader.TokenType switch
    {
        JsonTokenType.StartObject => new JsObject(),
        JsonTokenType.StartArray => new JsArray(),
        JsonTokenType.String => JsValue.From(Text(ref reader)),
        JsonTokenType.Number => JsValue.From(double.Parse(reader.ValueSpan, NumberStyles.Float, CultureInfo.InvariantCulture)),
        JsonTokenType.True => JsValue.From(value: true),
        JsonTokenType.False => JsValue.From(value: false),
        JsonTokenType.Null or JsonTokenType.None or JsonTokenType.EndObject or JsonTokenType.EndArray
            or JsonTokenType.PropertyName or JsonTokenType.Comment => JsValue.Null,
        _ => JsValue.Null,
    };

    private static string Text(ref Utf8JsonReader reader) =>
        reader.ValueIsEscaped ? Unescape(reader.ValueSpan) : Encoding.UTF8.GetString(reader.ValueSpan);

    // The reader has already checked every escape, so each one is complete and well formed.
    private static string Unescape(ReadOnlySpan<byte> raw)
    {
        var text = new StringBuilder(raw.Length);
        while (raw.Length > 0)
        {
            var slash = raw.IndexOf((byte)'\\');
            if (slash < 0)
            {
                _ = text.Append(Encoding.UTF8.GetString(raw));
                break;
            }

            _ = text.Append(Encoding.UTF8.GetString(raw[..slash]));
            var kind = raw[slash + 1];
            _ = kind == (byte)'u'
                ? text.Append((char)int.Parse(raw.Slice(slash + 2, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture))
                : text.Append(Escaped(kind));
            raw = raw[(slash + (kind == (byte)'u' ? 6 : 2))..];
        }

        return text.ToString();
    }

    private static char Escaped(byte kind) => kind switch
    {
        (byte)'b' => '\b',
        (byte)'f' => '\f',
        (byte)'n' => '\n',
        (byte)'r' => '\r',
        (byte)'t' => '\t',
        _ => (char)kind,   // '"', '\\' and '/' stand for themselves
    };
}
