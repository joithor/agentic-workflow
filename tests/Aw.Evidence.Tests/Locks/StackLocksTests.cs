using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Locks;

public sealed class StackLocksTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task The_qa_stack_is_taken_before_the_heavy_job_and_released_after_it()
    {
        var locks = new InMemoryMachineLocks();

        var lease = (await StackLocks.AcquireAsync(locks, TimeSpan.Zero, CancellationToken.None)).Value;
        Assert.Equal([LockKind.QaStack, LockKind.HeavyJob], lease.Held);
        await lease.DisposeAsync();

        Assert.Equal(["acquire QaStack", "acquire HeavyJob", "release HeavyJob", "release QaStack"], locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_heavy_job_lock_releases_the_qa_stack_it_already_took()
    {
        var locks = new InMemoryMachineLocks();
        _ = locks.AlwaysBusy.Add(LockKind.HeavyJob);

        var result = await StackLocks.AcquireAsync(locks, TimeSpan.Zero, CancellationToken.None);

        Assert.Equal(EvidenceErrors.LockBusy.Code, result.Error.Code);
        Assert.False(locks.IsHeld(LockKind.QaStack));
        Assert.Equal(["acquire QaStack", "release QaStack"], locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_qa_stack_takes_nothing()
    {
        var locks = new InMemoryMachineLocks();
        _ = locks.AlwaysBusy.Add(LockKind.QaStack);

        var result = await StackLocks.AcquireAsync(locks, TimeSpan.Zero, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Empty(locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Disposing_the_combined_lease_twice_releases_once()
    {
        var locks = new InMemoryMachineLocks();
        var lease = (await StackLocks.AcquireAsync(locks, TimeSpan.Zero, CancellationToken.None)).Value;

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Equal(2, locks.Events.Count(static e => e.StartsWith("release", StringComparison.Ordinal)));
    }
}
