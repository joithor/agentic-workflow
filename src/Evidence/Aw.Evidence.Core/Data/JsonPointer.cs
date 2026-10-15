using System.Globalization;
using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>A JSON Pointer (RFC 6901): its own small evaluator, so a script's assertions never depend on a library's dialect.</summary>
public static class JsonPointer
{
    /// <summary>Why a pointer is malformed, or null when it is well-formed ("" or '/'-led, '~0' and '~1' the only escapes).</summary>
    /// <param name="reference">The pointer.</param>
    /// <returns>The problem, or null.</returns>
    public static string? Problem(string reference)
    {
        if (reference.Length == 0)
        {
            return null;
        }

        if (reference[0] != '/')
        {
            return "a JSON Pointer is empty or starts with '/'";
        }

        for (var i = 0; i < reference.Length; i++)
        {
            if (reference[i] == '~' && (i + 1 >= reference.Length || reference[i + 1] is not ('0' or '1')))
            {
                return "'~' must be followed by 0 or 1 in a JSON Pointer";
            }
        }

        return null;
    }

    /// <summary>The value a pointer names in a document.</summary>
    /// <param name="document">The root element.</param>
    /// <param name="reference">A well-formed pointer.</param>
    /// <returns>The element, or null when it does not resolve (an array index is digits without a leading zero; '-' never resolves).</returns>
    public static JsonElement? Resolve(JsonElement document, string reference)
    {
        if (reference.Length == 0)
        {
            return document;
        }

        var current = document;
        foreach (var raw in reference[1..].Split('/'))
        {
            var token = raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (Step(current, token) is not { } next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static JsonElement? Step(JsonElement parent, string token) =>
        parent.ValueKind switch
        {
            JsonValueKind.Object => parent.TryGetProperty(token, out var member) ? member : null,
            JsonValueKind.Array => Index(token, parent.GetArrayLength()) is { } index ? parent[index] : null,
            JsonValueKind.Undefined or JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => null,
            _ => null,
        };

    // Digits only, no leading zero ('-' is the position after the last element and never names a value).
    private static int? Index(string token, int length)
    {
        var plain = token.Length > 0 && token.All(char.IsAsciiDigit) && (token.Length == 1 || token[0] != '0');
        return plain && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < length ? index : null;
    }
}
