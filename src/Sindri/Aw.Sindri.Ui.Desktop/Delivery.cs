using System.Net.Sockets;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>A message the listener read, its sender's connection, and whether the holder's reader handled it in time.</summary>
internal sealed class Delivery : IDisposable
{
    private readonly CancellationTokenSource _withdrawal = new();

    /// <summary>A message the listener read.</summary>
    /// <param name="message">The message's text.</param>
    /// <param name="sender">The sender's connection, open while the listener waits to answer it.</param>
    internal Delivery(string message, Socket sender)
    {
        Message = message;
        Sender = sender;
        Withdrawn = _withdrawal.Token; // taken now, so it stays readable after disposal
    }

    /// <summary>The message's text.</summary>
    internal string Message { get; }

    /// <summary>The sender's connection.</summary>
    internal Socket Sender { get; }

    /// <summary>True once handled; false once the sender's wait ended without it.</summary>
    internal TaskCompletionSource<bool> Handled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Cancelled once the listener has answered the sender or stopped waiting: the app no longer acts on it.</summary>
    internal CancellationToken Withdrawn { get; }

    /// <summary>Too late to act on: the wait ended, or the sender has gone.</summary>
    internal bool Stale => Handled.Task.IsCompleted || InstanceWire.Gone(Sender);

    /// <summary>Claims the message for acting on it (the app, on the UI thread) or as read (the reader came back).</summary>
    /// <returns>True for the claim that settles it as handled; false once the wait ended or it was claimed.</returns>
    internal bool TryAct() => Handled.TrySetResult(true);

    /// <summary>The wait has ended (answered, or given up): withdraws the message, unless it was claimed first.</summary>
    internal void Withdraw()
    {
        _ = Handled.TrySetResult(false); // unclaimed by now: the app doesn't act on it, and the reader skips it
        _withdrawal.Cancel();
    }

    public void Dispose() => _withdrawal.Dispose();
}
