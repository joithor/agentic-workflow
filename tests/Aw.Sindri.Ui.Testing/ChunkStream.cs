using System.Threading.Channels;

namespace Aw.Sindri.Ui.Testing;

/// <summary>A read-only stream fed chunk by chunk, so a canned response body can stay open like a live event stream.</summary>
internal sealed class ChunkStream : Stream
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
    private ReadOnlyMemory<byte> _rest;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    /// <summary>Sends a chunk to the reader.</summary>
    /// <param name="chunk">The bytes.</param>
    public void Send(byte[] chunk) => _ = _chunks.Writer.TryWrite(chunk);

    /// <summary>Ends the body.</summary>
    public void End() => _ = _chunks.Writer.TryComplete();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (_rest.IsEmpty)
        {
            if (!await _chunks.Reader.WaitToReadAsync(cancellationToken))
            {
                return 0;
            }

            _ = _chunks.Reader.TryRead(out var next);
            _rest = next;
        }

        var n = Math.Min(buffer.Length, _rest.Length);
        _rest[..n].CopyTo(buffer);
        _rest = _rest[n..];
        return n;
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
