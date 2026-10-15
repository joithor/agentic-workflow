using System.Text.Json;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Features.OutboxRecord;

/// <summary>Reads the PostToolUse hook's JSON for a message send. The payload is Claude Code's own format, so only the fields used are read.</summary>
public static class PostToolUseParser
{
    /// <summary>Parses the hook payload.</summary>
    /// <param name="json">The payload: session_id, tool_input with to and message (or summary) and optionally tool_response with a msg_id.</param>
    /// <returns>The request; null when the payload is a well-formed call of some other tool (no recipient or text); an error when it is not JSON or has no session.</returns>
    public static Result<OutboxRecordRequest?> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !SessionId.TryFrom(Text(root, "session_id") ?? string.Empty, out var session))
            {
                return MetaErrors.Invalid.With("the hook payload is not an object with a session_id");
            }

            return Results.Success(Send(root, session));
        }
        catch (JsonException)
        {
            return MetaErrors.Invalid.With("the hook payload is not JSON");
        }
    }

    private static OutboxRecordRequest? Send(JsonElement root, SessionId session)
    {
        if (!root.TryGetProperty("tool_input", out var input) || input.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var to = Text(input, "to");
        var message = Text(input, "message") ?? Text(input, "summary");
        return to is null || message is null ? null : new OutboxRecordRequest(session, to, message, MessageIdOf(root));
    }

    private static MessageId? MessageIdOf(JsonElement root)
    {
        if (!root.TryGetProperty("tool_response", out var response) || response.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = Text(response, "msg_id") ?? Text(response, "message_id");
        return id is not null && MessageId.TryFrom(id, out var parsed) ? parsed : null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}
