using System.Globalization;
using System.Text;

namespace Aw.Kernel.Json;

/// <summary>
/// String escaping exactly as Node's 'JSON.stringify' does it (ECMAScript QuoteJSONString, ES2019+):
/// quote and backslash; backspace, tab, newline, form feed and carriage return by their short forms; every other
/// code unit below 0x20 and every lone surrogate as a lowercase hex escape. Everything else, including non-ASCII,
/// HTML characters, U+2028 and U+2029, is written as is.
/// </summary>
internal static class NodeJsonEscaping
{
    private static readonly string[] _controlEscapes = Build_controlEscapes();

    /// <summary>The escape for a quote, backslash or control code unit, or null when it is written as is.</summary>
    public static string? EscapeQuoteOrControl(int codeUnit) => codeUnit switch
    {
        '"' => "\\\"",
        '\\' => "\\\\",
        < 0x20 and >= 0 => _controlEscapes[codeUnit],
        _ => null,
    };

    /// <summary>The JSON string literal for <paramref name="value"/>, quotes included.</summary>
    public static string Quote(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                builder.Append(c).Append(value[++i]);
            }
            else if (char.IsSurrogate(c))
            {
                builder.Append(Hex(c));
            }
            else
            {
                AppendEscaped(builder, c);
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>Reverses JSON escapes in the raw text of a string token (quotes not included).</summary>
    public static string Unescape(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '\\')
            {
                builder.Append(raw[i]);
                continue;
            }

            var kind = raw[++i];
            if (kind == 'u')
            {
                builder.Append((char)int.Parse(raw.AsSpan(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                i += 4;
            }
            else
            {
                builder.Append(UnescapeShort(kind));
            }
        }

        return builder.ToString();
    }

    private static void AppendEscaped(StringBuilder builder, char c)
    {
        var escaped = EscapeQuoteOrControl(c);
        if (escaped is null)
        {
            builder.Append(c);
        }
        else
        {
            builder.Append(escaped);
        }
    }

    private static char UnescapeShort(char kind) => kind switch
    {
        'b' => '\b',
        'f' => '\f',
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        _ => kind, // the quote, backslash and slash escapes stand for themselves
    };

    private static string Hex(int codeUnit) => "\\u" + codeUnit.ToString("x4", CultureInfo.InvariantCulture);

    private static string[] Build_controlEscapes()
    {
        var escapes = new string[0x20];
        for (var i = 0; i < escapes.Length; i++)
        {
            escapes[i] = Hex(i);
        }

        escapes['\b'] = "\\b";
        escapes['\t'] = "\\t";
        escapes['\n'] = "\\n";
        escapes['\f'] = "\\f";
        escapes['\r'] = "\\r";
        return escapes;
    }
}
