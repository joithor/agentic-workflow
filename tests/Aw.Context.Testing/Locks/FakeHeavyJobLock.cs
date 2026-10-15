using Aw.Context.Core;
using Aw.Context.Core.Locks;
using Aw.Kernel;

namespace Aw.Context.Testing.Locks;

/// <summary>The in-memory <see cref="IHeavyJobLock"/>: one slot, or busy when <see cref="BusyHolder"/> names a holder. <see cref="HeavyJobLockContract"/> holds it to the adapters' behavior.</summary>
public sealed class FakeHeavyJobLock : IHeavyJobLock
{
    /// <summary>When set, another holder has every slot: <see cref="Take"/> reports it, then answers SND-CONTEXT-040 naming it.</summary>
    public string? BusyHolder { get; set; }

    /// <summary>How many slots are held right now.</summary>
    public int Held { get; private set; }

    /// <summary>How many times a slot was taken.</summary>
    public int Taken { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Result<IAsyncDisposable>> Take(IProgress<string>? waiting, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (BusyHolder is { } holder)
        {
            waiting?.Report($"waiting for heavy-job lock (slot 1/2 held by {holder})");
            return ValueTask.FromResult<Result<IAsyncDisposable>>(ContextErrors.HeavyLockBusy.With($"the heavy-job lock stayed busy, held by {holder} (fix: wait for it, or run the build later)"));
        }

        Taken++;
        Held++;
        return ValueTask.FromResult<Result<IAsyncDisposable>>(new Slot(this));
    }

    private sealed class Slot(FakeHeavyJobLock owner) : IAsyncDisposable
    {
        private bool _released;

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                owner.Held--;
            }

            return ValueTask.CompletedTask;
        }
    }
}
