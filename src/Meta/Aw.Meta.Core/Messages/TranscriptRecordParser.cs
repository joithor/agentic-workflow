namespace Aw.Meta.Core.Messages;

/// <summary>Reads one transcript JSON line and yields the peer message it carries, if any.</summary>
public static class TranscriptRecordParser
{
    /// <summary>Parses one line.</summary>
    /// <param name="line">One JSONL line.</param>
    /// <param name="fallbackAt">The time to use when the record has no readable timestamp.</param>
    /// <returns>The peer message, or null for any other record or a line that is not JSON.</returns>
    public static PeerMessage? Parse(string line, DateTimeOffset fallbackAt) => throw new NotSupportedException("TranscriptRecordParser: make TranscriptRecordParserTests pass");
}
