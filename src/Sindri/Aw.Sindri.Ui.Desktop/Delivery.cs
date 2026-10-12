using System.Net.Sockets;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>A message the listener read, its sender's connection, and whether the holder's reader handled it in time.</summary>
/// <param name="Message">The message's text.</param>
/// <param name="Sender">The sender's connection, open while the listener waits to answer it.</param>
/// <param name="Handled">True once handled; false once the sender's wait ended without it.</param>
internal sealed record Delivery(string Message, Socket Sender, TaskCompletionSource<bool> Handled)
{
    /// <summary>Too late to act on: the wait ended, or the sender has gone.</summary>
    internal bool Stale => Handled.Task.IsCompleted || InstanceWire.Gone(Sender);
}
