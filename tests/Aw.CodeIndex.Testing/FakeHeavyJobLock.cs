using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>A heavy-job lock with a fixed number of slots; a taker past them gets SND-CODEIDX-004 at once, with no wait. Tests read <see cref="Held"/> to see the slot is released.</summary>
/// <param name="slots">How many slots exist.</param>
public sealed class FakeHeavyJobLock(int slots = 1) : IHeavyJobLock
{
    private int _held;

    /// <summary>How many slots are held now.</summary>
    public int Held => Volatile.Read(ref _held);

    /// <inheritdoc />
    public ValueTask<Result<IAsyncDisposable>> Take(string holder, TimeSpan maxWait, IProgress<string>? waiting, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _held) > slots)
        {
            _ = Interlocked.Decrement(ref _held);
            return ValueTask.FromResult<Result<IAsyncDisposable>>(CodeIndexErrors.HeavyBusy.With("every heavy-job slot is busy"));
        }

        return ValueTask.FromResult<Result<IAsyncDisposable>>(new Slot(this));
    }

    private sealed class Slot(FakeHeavyJobLock owner) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _ = Interlocked.Decrement(ref owner._held);
            }

            return ValueTask.CompletedTask;
        }
    }
}
