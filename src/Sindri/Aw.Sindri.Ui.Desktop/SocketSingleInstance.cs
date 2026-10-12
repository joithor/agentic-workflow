using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The desktop adapter of <see cref="ISingleInstance"/>, under the owner-only 'ui' directory: the claim is an exclusive
/// lock on instance.lock (the OS drops it when the process dies, so a crash leaves no claim behind), and the channel a
/// Unix domain socket, instance.sock (0600; AF_UNIX on Windows 10 and later too; in an owner-only directory under the
/// user's temp directory when the home is too deep for sun_path, <see cref="UiStatePaths.SocketIn"/>). Whoever holds the lock removes a socket
/// left by a crash and listens. A later launch connects, writes one message (UTF-8, at most
/// <see cref="InstanceMessages.MaxLength"/> characters), closes its side and reads one byte: 1 taken, 0 refused.
/// </summary>
/// <param name="paths">Where the lock and the socket live.</param>
/// <param name="clock">The clock for the channel's timeouts.</param>
internal sealed class SocketSingleInstance(UiStatePaths paths, TimeProvider clock) : ISingleInstance
{
    private const byte Taken = 1;
    private const byte Refused = 0;
    private const int MaxBytes = InstanceMessages.MaxLength * 4;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);
    private static readonly UTF8Encoding _strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly Channel<string> _inbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private FileStream? _claim;
    private Socket? _listener;
    private Task _accepting = Task.CompletedTask;

    public async ValueTask<bool> TryClaim(CancellationToken cancellationToken)
    {
        if (_claim is not null)
        {
            return true;
        }

        OwnerOnly.CreateDirectory(paths.Directory);
        OwnerOnly.CreateDirectory(Path.GetDirectoryName(paths.InstanceSocket)!); // why: SocketIn always joins a directory and a file
        FileStream claim;
        try
        {
            claim = OwnerOnly.Open(paths.InstanceLock, FileMode.OpenOrCreate, FileShare.None);
        }
        catch (IOException)
        {
            return false;
        }

        try
        {
            _listener = Listen();
        }
        catch
        {
            await claim.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _claim = claim;
        _accepting = Accept(_listener, _stop.Token);
        return true;
    }

    public async ValueTask<bool> Send(string message, CancellationToken cancellationToken)
    {
        if (message.Length is 0 or > InstanceMessages.MaxLength)
        {
            return false;
        }

        using var timeout = new CancellationTokenSource(_timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(paths.InstanceSocket), linked.Token).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(message).AsMemory();
            while (!bytes.IsEmpty)
            {
                bytes = bytes[await socket.SendAsync(bytes, SocketFlags.None, linked.Token).ConfigureAwait(false)..];
            }

            socket.Shutdown(SocketShutdown.Send);
            var answer = new byte[1];
            return await socket.ReceiveAsync(answer, SocketFlags.None, linked.Token).ConfigureAwait(false) == 1 && answer[0] == Taken;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    public async IAsyncEnumerable<string> Messages([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_claim is null)
        {
            yield break;
        }

        await foreach (var message in _inbox.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return message;
        }
    }

    public int? HolderProcessId() => null;

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        await _accepting.ConfigureAwait(false);
        if (_claim is not null)
        {
            File.Delete(paths.InstanceSocket); // while the lock is still held, so it can't be a later instance's
            await _claim.DisposeAsync().ConfigureAwait(false);
        }

        _ = _inbox.Writer.TryComplete();
        _stop.Dispose();
    }

    // Holding the lock means no instance listens, so a socket file here is a crashed one's: replace it.
    private Socket Listen()
    {
        File.Delete(paths.InstanceSocket);
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(paths.InstanceSocket));
            OwnerOnly.Restrict(paths.InstanceSocket);
            listener.Listen(backlog: 8);
            return listener;
        }
        catch
        {
            listener.Dispose();
            throw;
        }
    }

    private async Task Accept(Socket listener, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await listener.AcceptAsync(stop).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            // One at a time: a sender waits for its answer before the next send, so messages keep their order.
            await Serve(client, stop).ConfigureAwait(false);
        }
    }

    private async Task Serve(Socket client, CancellationToken stop)
    {
        using (client)
        {
            using var timeout = new CancellationTokenSource(_timeout, clock);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop, timeout.Token);
            try
            {
                var message = Decode(await ReadAll(client, linked.Token).ConfigureAwait(false));
                var taken = message is not null && _inbox.Writer.TryWrite(message);
                _ = await client.SendAsync(new[] { taken ? Taken : Refused }, SocketFlags.None, linked.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException)
            {
                // A sender that hangs or goes away gets no answer; the listener keeps serving.
            }
        }
    }

    // Reads to the sender's end of stream, stopping one byte past the limit.
    private static async Task<ArraySegment<byte>> ReadAll(Socket client, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxBytes + 1];
        var total = 0;
        int read;
        while (total < buffer.Length && (read = await client.ReceiveAsync(buffer.AsMemory(total), SocketFlags.None, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
        }

        return new ArraySegment<byte>(buffer, 0, total);
    }

    // The message, or null when it is empty, over the limit or not UTF-8.
    private static string? Decode(ArraySegment<byte> bytes)
    {
        if (bytes.Count is 0 or > MaxBytes)
        {
            return null;
        }

        try
        {
            var text = _strict.GetString(bytes);
            return text.Length <= InstanceMessages.MaxLength ? text : null;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
