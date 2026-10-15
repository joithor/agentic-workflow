using Aw.Kernel;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Core.Ports;

/// <summary>Reads a session transcript incrementally.</summary>
public interface ITranscriptReader
{
    /// <summary>Reads the complete lines from <paramref name="offset"/> to the end. A trailing partial line is left unread.</summary>
    /// <param name="path">The transcript file.</param>
    /// <param name="offset">The byte offset to start at; past the end of a shorter file, the read starts at 0.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines and the offset after them, or AW-META-004 when the file can't be read.</returns>
    public ValueTask<Result<TranscriptChunk>> ReadFrom(string path, long offset, CancellationToken cancellationToken);
}
