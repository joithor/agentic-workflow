using System.Text;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// ITranscriptReader over a transcript file. The file is opened for sharing because Claude Code is appending to it; the
/// offset counts bytes, and a trailing line without its newline is left for the next read.
/// </summary>
internal sealed class FileTranscriptReader : ITranscriptReader
{
    private const byte NewLine = (byte)'\n';

    public async ValueTask<Result<TranscriptChunk>> ReadFrom(string path, long offset, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                var start = offset < 0 || offset > stream.Length ? 0 : offset;
                _ = stream.Seek(start, SeekOrigin.Begin);
                var bytes = new byte[stream.Length - start];
                await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                return Complete(bytes, start);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.TranscriptUnreadable.With($"{path}: {exception.Message}");
        }
    }

    private static TranscriptChunk Complete(byte[] bytes, long start)
    {
        var lines = new List<string>();
        var from = 0;
        for (var at = Array.IndexOf(bytes, NewLine); at >= 0; at = Array.IndexOf(bytes, NewLine, from))
        {
            lines.Add(Encoding.UTF8.GetString(bytes, from, at - from).TrimEnd('\r'));
            from = at + 1;
        }

        return new TranscriptChunk(lines, start + from);
    }
}
