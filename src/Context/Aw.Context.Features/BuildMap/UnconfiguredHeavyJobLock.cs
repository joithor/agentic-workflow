using Aw.Context.Core;
using Aw.Context.Core.Locks;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The lock a host that registered no <see cref="IHeavyJobLock"/> gets: it never grants a slot, so a backtest never runs without the box-wide lock (SND-CONTEXT-040).</summary>
internal sealed class UnconfiguredHeavyJobLock : IHeavyJobLock
{
    public ValueTask<Result<IAsyncDisposable>> Take(IProgress<string>? waiting, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IAsyncDisposable>>(
            ContextErrors.HeavyLockBusy.With("this host has no heavy-job lock set up, and a map build never runs without one (fix: build from a host that wires the lock)"));
}
