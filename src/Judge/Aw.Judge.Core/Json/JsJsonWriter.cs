using System.Globalization;
using System.Text;

namespace Aw.Judge.Core.Json;

/// <summary>JSON.stringify: JavaScript's number text, its string escapes (only quotes, backslashes, control characters
/// and lone surrogates; lowercase hex), and with indentation two spaces, "key": value, empty containers as {} and [].</summary>
internal static class JsJsonWriter
{
    private const string Step = "  ";

    internal static string Write(JsValue value, bool indented)
    {
        var json = new StringBuilder();
        Append(json, value, indented ? "" : null);
        return json.ToString();
    }

    private static void Append(StringBuilder json, JsValue value, string? indent)
    {
        switch (value)
        {
            case JsObject o:
                AppendObject(json, o, indent);
                break;
            case JsArray a:
                AppendArray(json, a, indent);
                break;
            case JsString s:
                AppendString(json, s.Value);
                break;
            case JsNumber n:
                _ = json.Append(double.IsFinite(n.Value) ? JsNumbers.Format(n.Value) : "null");
                break;
            case JsBoolean b:
                _ = json.Append(b.Value ? "true" : "false");
                break;
            default:
                _ = json.Append("null");
                break;
        }
    }

    private static void AppendObject(StringBuilder json, JsObject value, string? indent)
    {
        var inner = indent is null ? null : indent + Step;
        _ = json.Append('{');
        var first = true;
        foreach (var (key, item) in value.Properties)
        {
            Separate(json, inner, ref first);
            AppendString(json, key);
            _ = json.Append(indent is null ? ":" : ": ");
            Append(json, item, inner);
        }

        Close(json, indent, empty: first, '}');
    }

    private static void AppendArray(StringBuilder json, JsArray value, string? indent)
    {
        var inner = indent is null ? null : indent + Step;
        _ = json.Append('[');
        var first = true;
        foreach (var item in value.Items)
        {
            Separate(json, inner, ref first);
            Append(json, item, inner);
        }

        Close(json, indent, empty: first, ']');
    }

    private static void Separate(StringBuilder json, string? inner, ref bool first)
    {
        if (!first)
        {
            _ = json.Append(',');
        }

        first = false;
        if (inner is not null)
        {
            _ = json.Append('\n').Append(inner);
        }
    }

    private static void Close(StringBuilder json, string? indent, bool empty, char bracket)
    {
        if (!empty && indent is not null)
        {
            _ = json.Append('\n').Append(indent);
        }

        _ = json.Append(bracket);
    }

    private static void AppendString(StringBuilder json, string text)
    {
        _ = json.Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                _ = json.Append(c).Append(text[i + 1]);
                i++;
            }
            else
            {
                _ = Escape(c) is { } escaped ? json.Append(escaped) : json.Append(c);
            }
        }

        _ = json.Append('"');
    }

    private static string? Escape(char c) => c switch
    {
        '"' => "\\\"",
        '\\' => "\\\\",
        '\b' => "\\b",
        '\f' => "\\f",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        _ when c < ' ' || char.IsSurrogate(c) => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
        _ => null,
    };
}
