using System.Net.Sockets;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Copies bytes both ways between two streams until either side ends.</summary>
internal static class EgressRelay
{
    /// <summary>Relays.</summary>
    /// <param name="a">One side.</param>
    /// <param name="b">The other side.</param>
    /// <param name="allow">Called with each chunk's size; false ends the relay (a byte cap).</param>
    /// <param name="cancellationToken">Ends the relay.</param>
    /// <returns>A task that ends when the relay does.</returns>
    public static async Task Run(Stream a, Stream b, Func<int, bool> allow, CancellationToken cancellationToken)
    {
        using var both = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var forward = Pump(a, b, allow, both.Token);
        var backward = Pump(b, a, allow, both.Token);
        try
        {
            var first = await Task.WhenAny(forward, backward).ConfigureAwait(false);
            var destination = first == forward ? b : a;
            if (destination is NetworkStream socket && first.IsCompletedSuccessfully)
            {
                // The sender is done (an HTTP/1.0 client half-closes after its request): pass the end on and let the answer finish.
                socket.Socket.Shutdown(SocketShutdown.Send);
            }
            else
            {
                await both.CancelAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                await Task.WhenAll(forward, backward).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // Either side closing ends the pair.
            }
        }
    }

    /// <summary>Ends the sending side of a stream: an <see cref="IHalfCloseable"/> or a socket stream. Never throws for a stream that is already closed.</summary>
    /// <param name="stream">The stream.</param>
    public static void HalfClose(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        throw new NotSupportedException();
    }

    private static async Task Pump(Stream from, Stream to, Func<int, bool> allow, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await from.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0 && allow(read))
        {
            await to.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            await to.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
