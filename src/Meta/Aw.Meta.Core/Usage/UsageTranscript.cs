using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aw.Meta.Core.Usage;

/// <summary>Reads the end of a Claude Code transcript for a usage-limit stop.</summary>
internal static class UsageTranscript
{
    /// <summary>Reads the transcript's last turn.</summary>
    /// <param name="lines">The last lines of the transcript, oldest first.</param>
    /// <param name="reference">When the transcript was last written; used when a line carries no time.</param>
    /// <param name="local">The zone of a reset time that names none.</param>
    /// <returns>The stop when the last user or assistant entry is an assistant usage-limit message; otherwise null.</returns>
    internal static UsageStop? Read(IReadOnlyList<string> lines, DateTimeOffset reference, TimeZoneInfo local)
    {
        ArgumentNullException.ThrowIfNull(lines);
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (Turn(lines[i]) is not { } turn)
            {
                continue;
            }

            return turn.Assistant && UsageLimitParser.Mentions(turn.Text) ? UsageLimitParser.Parse(turn.Text, turn.At ?? reference, local) : null;
        }

        return null;
    }

    // The entry as a turn, or null for a line that is not JSON or not a user or assistant entry.
    private static (bool Assistant, string Text, DateTimeOffset? At)? Turn(string line)
    {
        if (line.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var kind = type.GetString();
            if (kind is not ("user" or "assistant"))
            {
                return null;
            }

            var at = root.TryGetProperty("timestamp", out var stamp) && stamp.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(stamp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : (DateTimeOffset?)null;
            return (kind == "assistant", Text(root), at);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Text(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        return content.ValueKind == JsonValueKind.Array ? Pieces(content) : string.Empty;
    }

    private static string Pieces(JsonElement content)
    {
        var text = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var piece) && piece.ValueKind == JsonValueKind.String)
            {
                _ = text.Append(piece.GetString()).Append('\n');
            }
        }

        return text.ToString();
    }
}
