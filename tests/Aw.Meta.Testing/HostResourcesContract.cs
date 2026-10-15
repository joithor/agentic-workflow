using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IHostResources contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class HostResourcesContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>The adapter under test, with no lock held.</summary>
    /// <returns>The adapter.</returns>
    protected abstract IHostResources Create();

    /// <summary>Takes the next heavy-job lock slot for a holder pid.</summary>
    /// <param name="resources">The adapter returned by <see cref="Create"/>.</param>
    /// <param name="pid">The holder.</param>
    protected abstract void TakeLock(IHostResources resources, int pid);

    /// <summary>The figures are positive and consistent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Memory_and_disk_are_reported()
    {
        var read = await Create().Read(_none);

        Expect.That(read.IsSuccess, "the figures are read");
        Expect.That(read.Value.MemTotalKb > 0 && read.Value.MemAvailableKb > 0 && read.Value.MemAvailableKb <= read.Value.MemTotalKb, "available memory is within the total");
        Expect.That(read.Value.DiskFreeBytes > 0 && read.Value.AvailablePercent is > 0 and <= 100, "disk and percentage are real numbers");
    }

    /// <summary>With no slot taken there are no holders.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_free_lock_has_no_holders()
    {
        var locks = await Create().HeavyLocks(_none);

        Expect.That(locks.IsSuccess && locks.Value.Count == 0, "an unheld lock lists nothing");
    }

    /// <summary>A taken slot lists its holder and whether that process still runs.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Held_slots_list_their_holders_and_whether_they_run()
    {
        var resources = Create();
        TakeLock(resources, Environment.ProcessId);
        TakeLock(resources, 2_000_000_000);

        var locks = await resources.HeavyLocks(_none);

        Expect.That(locks.IsSuccess && locks.Value.Count == 2, "both slots are listed");
        Expect.That(locks.Value[0].Pid == Environment.ProcessId && locks.Value[0].Alive, "a running holder is alive");
        Expect.That(locks.Value[1].Pid == 2_000_000_000 && !locks.Value[1].Alive, "a vanished holder is stale");
    }
}
