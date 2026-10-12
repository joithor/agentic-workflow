using System.Threading.Channels;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The local channel every launch of one user shares (the fake's stand-in for the desktop's socket path). With a clock,
/// a send waits 1.5 s on it for the handling, like the desktop adapter, and then withdraws the message; without one it
/// waits until the holder handles the message (or the send is cancelled).
/// </summary>
/// <param name="clock">The clock the handling wait runs on, or null for no time limit.</param>
internal sealed class FakeInstanceChannel(TimeProvider? clock = null)
{
    /// <summary>How long a send waits for the handling (the desktop adapter's 1.5 s).</summary>
    internal static readonly TimeSpan HandleWithin = TimeSpan.FromMilliseconds(1500);

    private readonly Lock _gate = new();
    private FakeSingleInstance? _holder;
    private Channel<FakeDelivery>? _inbox;
    private bool _reading;

    /// <summary>The holder's process id, or null when no launch holds the claim.</summary>
    internal int? HolderProcessId
    {
        get
        {
            lock (_gate)
            {
                return _holder?.ProcessId;
            }
        }
    }

    internal bool TryClaim(FakeSingleInstance launch)
    {
        lock (_gate)
        {
            if (_holder is not null)
            {
                return ReferenceEquals(_holder, launch);
            }

            (_holder, _inbox) = (launch, Channel.CreateUnbounded<FakeDelivery>());
            return true;
        }
    }

    /// <summary>Whether a holder reads its messages now.</summary>
    internal bool Reading
    {
        get
        {
            lock (_gate)
            {
                return _reading;
            }
        }
    }

    /// <summary>How many messages the holder's reader has taken.</summary>
    internal int Taken => Volatile.Read(ref _taken);

    private int _taken;

    // The delivery the holder will read; null at once when no holder reads its messages.
    internal FakeDelivery? Deliver(string message)
    {
        lock (_gate)
        {
            if (_inbox is null || !_reading)
            {
                return null;
            }

            var delivery = new FakeDelivery(message);
            if (_inbox.Writer.TryWrite(delivery))
            {
                return delivery;
            }

            delivery.Dispose();
            return null;
        }
    }

    // The sender's handling window, started before its message is delivered so no clock move is missed: HandleWithin on
    // the clock, or none.
    internal CancellationTokenSource HandlingWindow() => clock is null ? new CancellationTokenSource() : new CancellationTokenSource(HandleWithin, clock);

    internal void Took() => _ = Interlocked.Increment(ref _taken);

    internal ChannelReader<FakeDelivery>? Inbox(FakeSingleInstance launch)
    {
        lock (_gate)
        {
            if (!ReferenceEquals(_holder, launch) || _inbox is null)
            {
                return null;
            }

            _reading = true;
            return _inbox.Reader;
        }
    }

    internal void StopReading(FakeSingleInstance launch)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_holder, launch))
            {
                _reading = false;
            }
        }
    }

    internal void Release(FakeSingleInstance launch)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_holder, launch))
            {
                _ = _inbox?.Writer.TryComplete();
                (_holder, _inbox, _reading) = (null, null, false);
            }
        }
    }
}
