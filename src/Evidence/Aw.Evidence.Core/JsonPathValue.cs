using System.Globalization;
using System.Text.Json;

namespace Aw.Evidence.Core;

/// <summary>The value a System.Text.Json error path ("$.steps[0]['odd.key'].kind") points at, as raw JSON text.</summary>
/// <param name="Kind">The value's JSON kind.</param>
/// <param name="Text">The value's raw JSON text.</param>
internal sealed record JsonPathValue(JsonValueKind Kind, string Text)
{
    /// <summary>Finds the value at <paramref name="path"/>.</summary>
    /// <param name="utf8Json">The document.</param>
    /// <param name="path">The path, starting with "$".</param>
    /// <returns>The value, or null when the document doesn't parse or the path leads nowhere.</returns>
    internal static JsonPathValue? Find(ReadOnlySpan<byte> utf8Json, string path)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json.ToArray());
            var at = document.RootElement;
            var i = 1;
            while (i < path.Length)
            {
                var (step, next) = NextStep(path, i);
                if (step is null || !TryStep(at, step, out at))
                {
                    return null;
                }

                i = next;
            }

            return new JsonPathValue(at.ValueKind, at.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A step is a property name or "[n]" for an array index. Returns null when the path is malformed.
    private static (string? Step, int Next) NextStep(string path, int i)
    {
        if (path[i] == '.')
        {
            var end = path.IndexOfAny(['.', '['], i + 1);
            end = end < 0 ? path.Length : end;
            return (path[(i + 1)..end], end);
        }

        if (path.AsSpan(i).StartsWith("['", StringComparison.Ordinal))
        {
            var end = path.IndexOf("']", i + 2, StringComparison.Ordinal);
            return end < 0 ? (null, path.Length) : (path[(i + 2)..end], end + 2);
        }

        if (path[i] == '[')
        {
            var end = path.IndexOf(']', i);
            return end < 0 ? (null, path.Length) : (path[i..(end + 1)], end + 1);
        }

        return (null, path.Length);
    }

    private static bool TryStep(JsonElement at, string step, out JsonElement value)
    {
        value = default;
        if (step.StartsWith('[') && at.ValueKind == JsonValueKind.Array)
        {
            return int.TryParse(step.AsSpan(1, step.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                && index < at.GetArrayLength()
                && (value = at[index]).ValueKind != JsonValueKind.Undefined;
        }

        return at.ValueKind == JsonValueKind.Object && at.TryGetProperty(step, out value);
    }
}
