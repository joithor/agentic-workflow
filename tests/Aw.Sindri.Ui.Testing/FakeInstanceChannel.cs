using System.Threading.Channels;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The local channel every launch of one user shares (the fake's stand-in for the desktop's socket path).</summary>
internal sealed class FakeInstanceChannel
{
    private readonly Lock _gate = new();
    private FakeSingleInstance? _holder;
    private Channel<string>? _inbox;

    internal bool TryClaim(FakeSingleInstance launch)
    {
        lock (_gate)
        {
            if (_holder is not null)
            {
                return ReferenceEquals(_holder, launch);
            }

            (_holder, _inbox) = (launch, Channel.CreateUnbounded<string>());
            return true;
        }
    }

    internal bool Deliver(string message)
    {
        lock (_gate)
        {
            return _inbox is not null && _inbox.Writer.TryWrite(message);
        }
    }

    internal ChannelReader<string>? Inbox(FakeSingleInstance launch)
    {
        lock (_gate)
        {
            return ReferenceEquals(_holder, launch) ? _inbox?.Reader : null;
        }
    }

    internal void Release(FakeSingleInstance launch)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_holder, launch))
            {
                _ = _inbox?.Writer.TryComplete();
                (_holder, _inbox) = (null, null);
            }
        }
    }
}
