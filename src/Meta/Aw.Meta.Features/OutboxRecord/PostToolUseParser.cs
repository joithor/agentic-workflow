using Aw.Kernel;

namespace Aw.Meta.Features.OutboxRecord;

/// <summary>Reads the PostToolUse hook's JSON for a message send.</summary>
public static class PostToolUseParser
{
    /// <summary>Parses the hook payload.</summary>
    /// <param name="json">The payload: session_id, tool_input with to and message (or summary) and optionally tool_response with a msg_id.</param>
    /// <returns>The request; null when the payload is a well-formed call of some other tool (no recipient or text); an error when it is not JSON or has no session.</returns>
    public static Result<OutboxRecordRequest?> Parse(string json) => throw new NotSupportedException();
}
