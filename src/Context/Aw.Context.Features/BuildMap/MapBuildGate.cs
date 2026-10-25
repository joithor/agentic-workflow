namespace Aw.Context.Features.BuildMap;

/// <summary>
/// One map build at a time per host (a host has one map store): a second build waits for the first instead of failing, so two refreshes of one map
/// never write over each other. One instance per host. The box-wide heavy-job lock is not used: a map build is mostly git reads and a model call.
/// </summary>
internal sealed class MapBuildGate : IDisposable
{
    private readonly SemaphoreSlim _one = new(1, 1);

    /// <summary>Waits for the running build, if any, and holds the gate until the returned lease is disposed.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The lease.</returns>
    public async ValueTask<IDisposable> Enter(CancellationToken cancellationToken)
    {
        await _one.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_one);
    }

    public void Dispose() => _one.Dispose();

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _ = gate.Release();
            }
        }
    }
}
