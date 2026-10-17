namespace Aw.Meta.Core.Gate;

/// <summary>
/// A fixed number of heavy slots shared by every gate of a train. A free slot goes to the waiter that comes first by train
/// position, then by step order, then by arrival.
/// </summary>
/// <param name="limit">The most slots held at once (at least 1).</param>
public sealed class SlotPool(int limit)
{
    private readonly Lock _gate = new();
    private readonly List<Waiter> _waiting = [];
    private int _free = Math.Max(1, limit);
    private long _arrivals;

    /// <summary>Waits for a slot.</summary>
    /// <param name="job">The train position of the gate asking (lower is served first).</param>
    /// <param name="step">The step's index within its gate (lower is served first).</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The held slot; disposing it hands the slot to the next waiter.</returns>
    public async ValueTask<IDisposable> Acquire(int job, int step, CancellationToken cancellationToken)
    {
        Waiter waiter;
        lock (_gate)
        {
            if (_free > 0)
            {
                _free--;
                return new Lease(this);
            }

            waiter = new Waiter(job, step, _arrivals++);
            _waiting.Add(waiter);
        }

        await using var registration = cancellationToken.Register(() => Withdraw(waiter)).ConfigureAwait(false);
        await waiter.Granted.Task.ConfigureAwait(false);
        return new Lease(this);
    }

    private void Withdraw(Waiter waiter)
    {
        bool withdrawn;
        lock (_gate)
        {
            withdrawn = _waiting.Remove(waiter);
        }

        if (withdrawn)
        {
            _ = waiter.Granted.TrySetCanceled();
        }
    }

    private void Release()
    {
        Waiter? next;
        lock (_gate)
        {
            next = _waiting.MinBy(static w => (w.Job, w.Step, w.Arrival));
            if (next is null)
            {
                _free++;
            }
            else
            {
                _ = _waiting.Remove(next);
            }
        }

        _ = next?.Granted.TrySetResult();
    }

    private sealed record Waiter(int Job, int Step, long Arrival)
    {
        internal TaskCompletionSource Granted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Lease(SlotPool pool) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                pool.Release();
            }
        }
    }
}
