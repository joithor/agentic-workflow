namespace Aw.Sindri.Core.Ledger;

/// <summary>The in-process <see cref="ILedgerSignal"/>: one per process, registered by AddSindriCore.</summary>
/// <param name="clock">Times the wait's limit.</param>
internal sealed class LedgerSignal(TimeProvider clock) : ILedgerSignal
{
    private readonly Lock _gate = new();
    private TaskCompletionSource _pulsed = NewPulse();
    private long _latest;

    public void Pulse(long sequence)
    {
        TaskCompletionSource woken;
        lock (_gate)
        {
            _latest = Math.Max(_latest, sequence);
            woken = _pulsed;
            _pulsed = NewPulse();
        }

        _ = woken.TrySetResult();   // continuations run on the pool (RunContinuationsAsynchronously), never on the appender
    }

    public async ValueTask<long> WaitAfter(long sequence, TimeSpan limit, CancellationToken cancellationToken)
    {
        using var expiry = new CancellationTokenSource(limit < TimeSpan.Zero ? TimeSpan.Zero : limit, clock);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, expiry.Token);
        while (true)
        {
            Task next;
            lock (_gate)
            {
                if (_latest > sequence)
                {
                    return _latest;
                }

                next = _pulsed.Task;
            }

            try
            {
                await next.WaitAsync(either.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lock (_gate)
                {
                    return _latest;   // the limit passed with no pulse past the cursor
                }
            }
        }
    }

    private static TaskCompletionSource NewPulse() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
