namespace Aw.Sindri.Tests.GetInstanceEvents;

/// <summary>A stdout that counts its writes, so a test can wait for the Nth one.</summary>
internal sealed class SignalOnWrite : StringWriter
{
    private readonly SemaphoreSlim _written = new(0);
    private int _writes;

    public override async Task WriteAsync(string? value)
    {
        await base.WriteAsync(value);
        _ = Interlocked.Increment(ref _writes);
        _ = _written.Release();
    }

    /// <summary>Waits until at least <paramref name="count"/> writes have happened.</summary>
    /// <param name="count">The write count to wait for.</param>
    public async Task Wait(int count)
    {
        while (Volatile.Read(ref _writes) < count)
        {
            _ = await _written.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Whether <paramref name="count"/> writes have happened within <paramref name="patience"/>.</summary>
    /// <param name="count">The write count to wait for.</param>
    /// <param name="patience">How long to wait.</param>
    public async Task<bool> Wait(int count, TimeSpan patience)
    {
        if (Volatile.Read(ref _writes) >= count)
        {
            return true;
        }

        _ = await _written.WaitAsync(patience, TestContext.Current.CancellationToken);
        return Volatile.Read(ref _writes) >= count;
    }
}
