using System.Threading.Channels;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The local channel every launch of one user shares (the fake's stand-in for the desktop's socket path).</summary>
internal sealed class FakeInstanceChannel
{
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

    // Whether the holder handled the message; false at once when no holder reads its messages.
    internal Task<bool> Deliver(string message)
    {
        lock (_gate)
        {
            if (_inbox is null || !_reading)
            {
                return Task.FromResult(false);
            }

            var delivery = new FakeDelivery(message, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            return _inbox.Writer.TryWrite(delivery) ? delivery.Handled.Task : Task.FromResult(false);
        }
    }

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
