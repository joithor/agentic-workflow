using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Transcripts;
using Aw.Kernel;

namespace Aw.Judge.Infrastructure.Transcripts;

/// <summary>ITranscriptReader over the Claude transcripts under ~/.claude/projects. Cutover unit 6.3 builds it.</summary>
internal sealed class ClaudeTranscriptReader : ITranscriptReader
{
    /// <inheritdoc />
    public ValueTask<Result<Transcript>> FindSession(SessionId session, CancellationToken cancellationToken)
        => throw new NotSupportedException("ClaudeTranscriptReader: cutover unit 6.3");

    /// <inheritdoc />
    public ValueTask<Result<Transcript>> Read(string path, CancellationToken cancellationToken)
        => throw new NotSupportedException("ClaudeTranscriptReader: cutover unit 6.3");
}
