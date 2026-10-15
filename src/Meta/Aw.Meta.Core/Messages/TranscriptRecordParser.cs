using System.Globalization;
using System.Text.Json;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Messages;

/// <summary>
/// Reads one transcript JSON line and yields the peer message it carries, if any. The transcript is Claude Code's own
/// format, not ours, so the record is read defensively: only the fields this needs, and anything unexpected is "no message".
/// </summary>
public static class TranscriptRecordParser
{
    /// <summary>Parses one line.</summary>
    /// <param name="line">One JSONL line.</param>
    /// <param name="fallbackAt">The time to use when the record has no readable timestamp.</param>
    /// <returns>The peer message, or null for any other record or a line that is not JSON.</returns>
    public static PeerMessage? Parse(string line, DateTimeOffset fallbackAt)
    {
        ArgumentNullException.ThrowIfNull(line);
        try
        {
            using var document = JsonDocument.Parse(line);
            var record = document.RootElement;
            if (record.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return Text(record, "type") switch
            {
                "user" => record.TryGetProperty("origin", out var origin) ? FromOrigin(origin, deliveryId: null, record, fallbackAt) : null,
                "attachment" => FromAttachment(record, fallbackAt),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PeerMessage? FromAttachment(JsonElement record, DateTimeOffset fallbackAt)
    {
        if (!record.TryGetProperty("attachment", out var attachment)
            || attachment.ValueKind != JsonValueKind.Object
            || Text(attachment, "type") != "queued_command"
            || !attachment.TryGetProperty("origin", out var origin))
        {
            return null;
        }

        return FromOrigin(origin, Text(attachment, "delivery_id"), record, fallbackAt);
    }

    private static PeerMessage? FromOrigin(JsonElement origin, string? deliveryId, JsonElement record, DateTimeOffset fallbackAt)
    {
        if (origin.ValueKind != JsonValueKind.Object || Text(origin, "kind") != "peer")
        {
            return null;
        }

        var id = Text(origin, "msg_id") ?? deliveryId;
        if (string.IsNullOrWhiteSpace(id) || !MessageId.TryFrom(id, out var messageId))
        {
            return null;
        }

        return new PeerMessage(
            messageId,
            Text(origin, "from") ?? string.Empty,
            Text(origin, "name") ?? string.Empty,
            Text(origin, "body") ?? string.Empty,
            Timestamp(record) ?? fallbackAt);
    }

    private static DateTimeOffset? Timestamp(JsonElement record) =>
        Text(record, "timestamp") is { } text
        && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at.ToUniversalTime()
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
