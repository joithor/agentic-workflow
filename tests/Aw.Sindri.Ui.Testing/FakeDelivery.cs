namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// A message in the fake channel: whether the holder handled it, and its withdrawal, like the desktop adapter's
/// delivery. The first of the app's claim (<see cref="TryAct"/>), the reader coming back, or the end of the sender's wait
/// (<see cref="Withdraw"/>) decides the answer.
/// </summary>
internal sealed class FakeDelivery : IDisposable
{
    private readonly CancellationTokenSource _withdrawal = new();

    /// <summary>A message the holder will read.</summary>
    /// <param name="message">The message's text.</param>
    public FakeDelivery(string message)
    {
        Message = message;
        Withdrawn = _withdrawal.Token; // taken now, so it stays readable after disposal
    }

    /// <summary>The message's text.</summary>
    public string Message { get; }

    /// <summary>True once handled; false once the sender's wait ended without it.</summary>
    public TaskCompletionSource<bool> Handled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Cancelled once the sender's wait has ended (answered, or given up).</summary>
    public CancellationToken Withdrawn { get; }

    /// <summary>Claims the message as handled (the app acting on it, or the reader coming back).</summary>
    /// <returns>True for the claim that settles it; false once withdrawn or claimed.</returns>
    public bool TryAct() => Handled.TrySetResult(true);

    /// <summary>The sender's wait has ended: withdraws the message, unless it was claimed first.</summary>
    public void Withdraw()
    {
        _ = Handled.TrySetResult(false);
        _withdrawal.Cancel();
    }

    public void Dispose() => _withdrawal.Dispose();
}
