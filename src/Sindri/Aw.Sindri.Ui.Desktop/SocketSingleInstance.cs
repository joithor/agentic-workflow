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
/// lock records its process id in instance.pid (0600), then removes a socket left by a crash and listens. A later launch
/// connects and sends one message (<see cref="InstanceWire"/>); the holder answers 1 only once its reader has handled it
/// (came back for the next one), within 1.5 s of its arrival; the sender waits 2 s. A message nothing handled in time,
/// or whose sender has gone, is dropped, never handed over late; one already handed over is withdrawn
/// (<see cref="InstanceMessage.Withdrawn"/>). The app claims a message on the UI thread before acting on it
/// (<see cref="InstanceMessage.TryAct"/>), and that claim, or the withdrawal if it came first, is the sender's answer.
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
    private bool _ended;
    private bool _retired;
    private Delivery? _serving;

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
#pragma warning disable CA2000 // kept in _claim and released by Dispose
            claim = OwnerOnly.Open(paths.InstanceLock, FileMode.OpenOrCreate, FileShare.None);
#pragma warning restore CA2000
        }
        catch (IOException)
        {
            return false;
        }

        // The process id first, so a launch that finds the lock held never reads a crashed predecessor's id (it names the
        // id when the holder doesn't answer).
        try
        {
            await File.WriteAllTextAsync(paths.InstancePid, Environment.ProcessId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            OwnerOnly.Restrict(paths.InstancePid);
            _listener = Listen();
        }
        catch
        {
            File.Delete(paths.InstancePid); // while the lock is still held, so it can't be a later instance's
            await claim.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _claim = claim;
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

    public async IAsyncEnumerable<InstanceMessage> Messages([EnumeratorCancellation] CancellationToken cancellationToken)
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
                yield return new InstanceMessage(delivery.Message, delivery.TryAct, delivery.Withdrawn);
            }
            finally
            {
                // The reader came back for the next one, or stopped after this one: either way it handled it, unless the
                // wait ended first.
                _ = delivery.TryAct();
            }
        }
    }

    public void Retire()
    {
        // From now on a message is refused at once; one the listener is serving is withdrawn. Serve checks the flag
        // after it publishes its delivery and Retire reads the delivery after it sets the flag, so one of the two sees
        // the other whatever the order.
        Volatile.Write(ref _retired, true);
        try
        {
            Volatile.Read(ref _serving)?.Withdraw();
        }
        catch (ObjectDisposedException)
        {
            // Answered and disposed meanwhile: nothing left to withdraw.
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
        // Ending twice is a no-op (SingleLaunch ends the instance it ran over; its owner may too).
        if (_ended)
        {
            return;
        }

        _ended = true;
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        await _accepting.ConfigureAwait(false);
        if (_claim is not null)
        {
            // While the lock is still held, so neither can be a later instance's. A directory removed while the app ran
            // (a temp home cleaned up, the home reset) is already cleaned up: the lock is released either way.
            try
            {
                File.Delete(paths.InstanceSocket);
                File.Delete(paths.InstancePid);
            }
            catch (DirectoryNotFoundException)
            {
                // Nothing left to remove.
            }

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
                delivery = message is null ? null : new Delivery(message, client);
                Volatile.Write(ref _serving, delivery);
                if (delivery is not null && Volatile.Read(ref _retired))
                {
                    delivery.Withdraw(); // the holder is ending: nothing will act on it
                }

                var handled = delivery is not null && !delivery.Handled.Task.IsCompleted && _inbox.Writer.TryWrite(delivery) && await Handled(delivery, linked.Token).ConfigureAwait(false);
                _ = await client.SendAsync(new[] { handled ? InstanceWire.Handled : InstanceWire.Refused }, SocketFlags.None, linked.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException)
            {
                // A sender that hangs or goes away gets no answer; the listener keeps serving.
            }
            finally
            {
                // Answered or given up: an unhandled message is skipped by the reader, and one already handed to the app
                // is withdrawn, so the app (which checks again on the UI thread) never acts on it late.
                delivery?.Withdraw();
                Volatile.Write(ref _serving, null);
                delivery?.Dispose();
            }
        }
    }

    // Waits for the reader to handle the message, until the sender leaves or 1.5 s pass. The answer is the one decision
    // the delivery settled on: the app's claim (TryAct, on the UI thread just before acting) or the reader coming back
    // wins if it came first; otherwise the withdrawal here does, and the app no longer acts on it (review run 4,
    // julik-frontend-races-reviewer-1).
    private async Task<bool> Handled(Delivery delivery, CancellationToken cancellationToken)
    {
        using var handling = new CancellationTokenSource(_handleWithin, clock);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, handling.Token);
        var left = InstanceWire.Left(delivery.Sender, waiting.Token);
        _ = await Task.WhenAny(delivery.Handled.Task, left).ConfigureAwait(false);
        await waiting.CancelAsync().ConfigureAwait(false);
        await left.ConfigureAwait(false);
        delivery.Withdraw();
        return await delivery.Handled.Task.ConfigureAwait(false);
    }
}
