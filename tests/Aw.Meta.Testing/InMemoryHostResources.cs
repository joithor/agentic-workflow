using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory IHostResources: the one fake, kept honest by HostResourcesContract.</summary>
public sealed class InMemoryHostResources : IHostResources
{
    private readonly List<HeavyLock> _locks = [];

    /// <summary>The figures <see cref="Read"/> returns; change them to simulate a loaded host.</summary>
    public HostResources Resources { get; set; } = new(MemTotalKb: 64_000_000, MemAvailableKb: 32_000_000, SwapUsedKb: 0, DiskFreeBytes: 50_000_000_000);

    /// <summary>Takes a lock slot for a holder; the holder counts as running when it is this process.</summary>
    /// <param name="pid">The holder's pid.</param>
    public void TakeLock(int pid)
    {
        var number = _locks.Count + 1;
        var path = number == 1 ? "/locks/heavy-job.lock" : string.Create(CultureInfo.InvariantCulture, $"/locks/heavy-job.lock.{number}");
        _locks.Add(new HeavyLock(path, pid, pid == Environment.ProcessId, string.Create(CultureInfo.InvariantCulture, $"{pid} held")));
    }

    /// <summary>Takes a slot as given.</summary>
    /// <param name="slot">The slot.</param>
    public void TakeLock(HeavyLock slot) => _locks.Add(slot);

    /// <inheritdoc />
    public ValueTask<Result<HostResources>> Read(CancellationToken cancellationToken) => Done.Of(Resources);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<HeavyLock>>> HeavyLocks(CancellationToken cancellationToken) =>
        Done.Of<IReadOnlyList<HeavyLock>>([.. _locks]);
}
