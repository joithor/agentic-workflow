using System.Text.Json;

namespace Aw.Meta.Core.Lanes;

/// <summary>Reads the context size out of the tail of a Claude Code transcript.</summary>
public static class TranscriptUsage
{
    /// <summary>
    /// Finds the last line that carries API usage and sums its input, cache-read and cache-creation tokens: the context window the
    /// last call used. A line that doesn't parse (a cut-off first or last line) or reports no tokens is skipped.
    /// </summary>
    /// <param name="lines">The transcript's lines, oldest first.</param>
    /// <returns>The tokens, or null when no line has usage.</returns>
    public static long? ContextTokens(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].Contains("\"usage\"", StringComparison.Ordinal) && Tokens(lines[i]) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static long? Tokens(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var usage = root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object && message.TryGetProperty("usage", out var inner)
                ? inner
                : root.TryGetProperty("usage", out var top) ? top : default;
            if (usage.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var total = Count(usage, "input_tokens") + Count(usage, "cache_read_input_tokens") + Count(usage, "cache_creation_input_tokens");
            return total > 0 ? total : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long Count(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number > 0 ? number : 0;
}
