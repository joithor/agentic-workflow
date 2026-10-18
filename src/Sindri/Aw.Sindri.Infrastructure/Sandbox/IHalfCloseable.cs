#pragma warning disable MA0182 // used by EgressRelay in the next commit
namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>A stream that can end its sending side and still be read: the relay's half-close seam (sockets adapt through <see cref="EgressRelay.HalfClose"/>).</summary>
internal interface IHalfCloseable
{
    /// <summary>Ends the sending side; the peer reads the end of stream.</summary>
    public void ShutdownSend();
}
#pragma warning restore MA0182
