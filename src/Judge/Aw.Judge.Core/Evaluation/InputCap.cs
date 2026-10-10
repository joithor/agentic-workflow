using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// Caps stored input and keeps it valid JSON (TS capJsonValue): instead of slicing the text, the longest string values
/// are shortened, each with its own "[truncated N chars]" marker, until the whole fits. Redact first: a secret cut at
/// the cap would no longer match its pattern.
/// </summary>
public static class InputCap
{
    /// <summary>The stored input's cap, in UTF-16 characters.</summary>
    public const int MaxChars = 16_000;

    // A string this short is never shortened: past that point the stub below is used.
    private const int ShrinkFloor = 64;

    /// <summary>Writes the value as JSON of at most <paramref name="maxChars"/> characters (for any cap above ~100).</summary>
    /// <param name="value">The (redacted) value; not changed.</param>
    /// <param name="maxChars">The cap.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>Valid JSON text.</returns>
    public static string Write(JsonNode? value, int maxChars, JsonSerializerOptions options)
    {
        var text = InputJson.Write(value, options);
        if (text.Length <= maxChars || value is null)
        {
            return text.Length <= maxChars ? text : Stub(text, maxChars, options);
        }

        var work = value.DeepClone();
        while (text.Length > maxChars)
        {
            var longest = Longest(work);
            if (longest is null || longest.GetValue<string>().Length <= ShrinkFloor)
            {
                // Nothing left to shorten (a huge array of numbers): keep a valid stub.
                return Stub(text, maxChars, options);
            }

            var current = longest.GetValue<string>();
            var keep = Math.Max(0, current.Length - (text.Length - maxChars) - ShrinkFloor);
            work = Replace(work, longest, JsonValue.Create(Truncate(current, keep)));
            text = InputJson.Write(work, options);
        }

        return text;
    }

    /// <summary>The text cut to <paramref name="maxChars"/> with a "[truncated N chars]" marker, or unchanged when it fits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="maxChars">How many characters to keep (one fewer when that would split a surrogate pair).</param>
    /// <returns>The capped text.</returns>
    public static string Truncate(string text, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= maxChars)
        {
            return text;
        }

        var keep = maxChars > 0 && char.IsHighSurrogate(text[maxChars - 1]) ? maxChars - 1 : maxChars;
        return string.Create(CultureInfo.InvariantCulture, $"{text[..keep]}[truncated {text.Length - keep} chars]");
    }

    // Puts the replacement where the old value was (an object keeps the key's position); the root is replaced whole.
    private static JsonNode Replace(JsonNode root, JsonValue old, JsonValue replacement)
    {
        switch (old.Parent)
        {
            case JsonObject parent:
                parent[old.GetPropertyName()] = replacement;
                return root;
            case JsonArray parent:
                parent[old.GetElementIndex()] = replacement;
                return root;
            default:
                return replacement;
        }
    }

    private static string Stub(string text, int maxChars, JsonSerializerOptions options) =>
        InputJson.Write(new JsonObject { ["truncated"] = Truncate(text, Math.Max(0, maxChars - 80)) }, options);

    // The first longest string value, depth first in document order.
    private static JsonValue? Longest(JsonNode? node)
    {
        JsonValue? best = null;
        foreach (var candidate in Strings(node))
        {
            if (best is null || candidate.GetValue<string>().Length > best.GetValue<string>().Length)
            {
                best = candidate;
            }
        }

        return best;
    }

    private static IEnumerable<JsonValue> Strings(JsonNode? node) => node switch
    {
        JsonObject obj => obj.SelectMany(pair => Strings(pair.Value)),
        JsonArray array => array.SelectMany(Strings),
        JsonValue value when value.GetValueKind() == JsonValueKind.String => [value],
        _ => [],
    };
}
