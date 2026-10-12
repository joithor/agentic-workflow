using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// The desktop adapter of <see cref="ISingleInstance"/>, under the owner-only 'ui' directory: the claim is an exclusive
/// lock on instance.lock (the OS drops it when the process dies, so a crash leaves no claim behind), and the channel a
/// Unix domain socket, instance.sock (0600; AF_UNIX on Windows 10 and later too; in an owner-only directory under the
/// user's temp directory when the home is too deep for sun_path, <see cref="UiStatePaths.SocketIn"/>). Whoever holds the
/// lock removes a socket left by a crash, listens, and records its process id in instance.pid (0600). A later launch
/// connects and sends one message (<see cref="InstanceWire"/>); the holder answers 1 only once its reader has handled it
/// (came back for the next one), within 1.5 s of its arrival; the sender waits 2 s. A message nothing handled in time,
/// or whose sender has gone, is dropped, never handed over late.
/// </summary>
internal sealed class SocketSingleInstance(UiStatePaths paths, TimeProvider clock) : ISingleInstance
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    // Shorter than the sender's 2 s, so a message handled in time is answered before the sender gives up.
    private static readonly TimeSpan _handleWithin = TimeSpan.FromMilliseconds(1500);
    private readonly Channel<Delivery> _inbox = Channel.CreateUnbounded<Delivery>(new UnboundedChannelOptions { SingleReader = true });
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
        await File.WriteAllTextAsync(paths.InstancePid, Environment.ProcessId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        OwnerOnly.Restrict(paths.InstancePid);
        _accepting = Accept(_listener, _stop.Token);
        return true;
    }

    public async ValueTask<bool> Send(string message, CancellationToken cancellationToken)
    {
        if (InstanceWire.Encode(message) is not { } wire)
        {
            return false;
        }

        using var timeout = new CancellationTokenSource(_timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(paths.InstanceSocket), linked.Token).ConfigureAwait(false);
            var bytes = wire.AsMemory();
            while (!bytes.IsEmpty)
            {
                bytes = bytes[await socket.SendAsync(bytes, SocketFlags.None, linked.Token).ConfigureAwait(false)..];
            }

            var answer = new byte[1];
            return await socket.ReceiveAsync(answer, SocketFlags.None, linked.Token).ConfigureAwait(false) == 1 && answer[0] == InstanceWire.Handled;
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

        await foreach (var delivery in _inbox.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            if (delivery.Stale)
            {
                _ = delivery.Handled.TrySetResult(false);
                continue; // its sender stopped waiting, or is about to: too late to act on
            }

            try
            {
                yield return delivery.Message;
            }
            finally
            {
                // The reader came back for the next one, or stopped after this one: either way it handled it.
                _ = delivery.Handled.TrySetResult(true);
            }
        }
    }

    public int? HolderProcessId()
    {
        // Only a launch whose claim failed asks, so the file is the live holder's (a crashed one's lock is gone and the
        // next holder rewrites it).
        try
        {
            return int.TryParse(File.ReadAllText(paths.InstancePid), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        await _accepting.ConfigureAwait(false);
        if (_claim is not null)
        {
            // While the lock is still held, so neither can be a later instance's.
            File.Delete(paths.InstanceSocket);
            File.Delete(paths.InstancePid);
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
            Delivery? delivery = null;
            try
            {
                var message = await InstanceWire.Read(client, linked.Token).ConfigureAwait(false);
                delivery = message is null ? null : new Delivery(message, client, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                var handled = delivery is not null && _inbox.Writer.TryWrite(delivery) && await Handled(delivery, linked.Token).ConfigureAwait(false);
                _ = await client.SendAsync(new[] { handled ? InstanceWire.Handled : InstanceWire.Refused }, SocketFlags.None, linked.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException)
            {
                // A sender that hangs or goes away gets no answer; the listener keeps serving.
            }
            finally
            {
                _ = delivery?.Handled.TrySetResult(false); // unhandled by now: the reader skips it
            }
        }
    }

    // Waits for the reader to handle the message, until the sender leaves or 1.5 s pass.
    private async Task<bool> Handled(Delivery delivery, CancellationToken cancellationToken)
    {
        using var handling = new CancellationTokenSource(_handleWithin, clock);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, handling.Token);
        var left = InstanceWire.Left(delivery.Sender, waiting.Token);
        var first = await Task.WhenAny(delivery.Handled.Task, left).ConfigureAwait(false);
        await waiting.CancelAsync().ConfigureAwait(false);
        await left.ConfigureAwait(false);
        return first == delivery.Handled.Task && await delivery.Handled.Task.ConfigureAwait(false);
    }
}
