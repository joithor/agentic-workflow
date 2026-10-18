using Aw.Kernel;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Ports;

/// <summary>Finds a Claude Code session's transcript (~/.claude/projects/&lt;project&gt;/&lt;session&gt;.jsonl) and reads only its end, since they run to many megabytes.</summary>
public interface IClaudeTranscripts
{
    /// <summary>Reads the tail of a session's transcript.</summary>
    /// <param name="session">The session id.</param>
    /// <param name="maxBytes">How many bytes from the end to read at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tail, or null when no project has a transcript for the session; AW-META-004 when the file can't be read.</returns>
    public ValueTask<Result<TranscriptTail?>> Tail(SessionId session, int maxBytes, CancellationToken cancellationToken);
}
