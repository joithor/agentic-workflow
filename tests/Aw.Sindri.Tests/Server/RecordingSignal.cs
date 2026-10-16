using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Tests.Server;

/// <summary>The in-process ledger signal, recording each wait it has entered, so a test can move the fake clock only once the pump is waiting.</summary>
/// <param name="clock">Times each wait's limit.</param>
internal sealed class RecordingSignal(TimeProvider clock) : ILedgerSignal
{
    private readonly LedgerSignal _inner = new(clock);
    private int _waits;

    /// <summary>The waits entered so far (the wait's timer is set by the time it counts).</summary>
    public int Waits => Volatile.Read(ref _waits);

    public void Pulse(long sequence) => _inner.Pulse(sequence);

    public async ValueTask<long> WaitAfter(long sequence, TimeSpan limit, CancellationToken cancellationToken)
    {
        var wait = _inner.WaitAfter(sequence, limit, cancellationToken);   // runs to its first await, so its limit is set on the clock
        _ = Interlocked.Increment(ref _waits);
        return await wait.ConfigureAwait(false);
    }

    /// <summary>Waits until the pump has entered its <paramref name="count"/>th wait.</summary>
    /// <param name="count">The wait to wait for.</param>
    /// <returns>When it has been entered.</returns>
    public async Task Entered(int count)
    {
        var until = RealTime.NowDateTime.AddSeconds(10);
        while (Waits < count)
        {
            Assert.True(RealTime.NowDateTime < until, "the pump never entered its wait");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}
