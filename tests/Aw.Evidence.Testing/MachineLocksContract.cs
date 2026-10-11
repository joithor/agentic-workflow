using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IMachineLocks contract: one holder per kind, kinds independent, a release lets the next holder in.</summary>
public abstract class MachineLocksContract
{
    /// <summary>Creates two handles on the same machine's locks (two processes, in the real adapter's terms).</summary>
    /// <returns>The handles.</returns>
    protected abstract (IMachineLocks First, IMachineLocks Second) CreatePair();

    /// <summary>A free lock is taken at once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_free_lock_is_taken()
    {
        var (locks, _) = CreatePair();

        await using var lease = (await locks.AcquireAsync(LockKind.QaStack, TimeSpan.Zero, CancellationToken.None)).Value;

        Expect.That(lease.Held.SequenceEqual([LockKind.QaStack]), "the lease holds the lock taken");
    }

    /// <summary>A held lock is busy for another holder once the timeout passes.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_held_lock_is_busy_for_another_holder()
    {
        var (first, second) = CreatePair();
        await using var held = (await first.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, CancellationToken.None)).Value;

        var busy = await second.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, CancellationToken.None);

        Expect.That(busy.IsError && busy.Error.Code == EvidenceErrors.LockBusy.Code, "a held lock is AW-EVIDENCE-009 for another holder");
    }

    /// <summary>The two kinds don't block each other.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Different_kinds_are_independent()
    {
        var (first, second) = CreatePair();
        await using var stack = (await first.AcquireAsync(LockKind.QaStack, TimeSpan.Zero, CancellationToken.None)).Value;

        var heavy = await second.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, CancellationToken.None);

        Expect.That(heavy.IsSuccess, "the other kind is free");
        await heavy.Value.DisposeAsync();
    }

    /// <summary>A released lock can be taken again; releasing twice is harmless.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_released_lock_can_be_taken_again()
    {
        var (first, second) = CreatePair();
        var lease = (await first.AcquireAsync(LockKind.QaStack, TimeSpan.Zero, CancellationToken.None)).Value;
        await lease.DisposeAsync();
        await lease.DisposeAsync();

        var again = await second.AcquireAsync(LockKind.QaStack, TimeSpan.Zero, CancellationToken.None);

        Expect.That(again.IsSuccess, "a released lock can be taken again");
        await again.Value.DisposeAsync();
    }

    /// <summary>A waiter gets the lock when the holder releases it within the timeout.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_waiter_gets_the_lock_when_it_is_released()
    {
        var (first, second) = CreatePair();
        var held = (await first.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, CancellationToken.None)).Value;

        var waiting = second.AcquireAsync(LockKind.HeavyJob, TimeSpan.FromSeconds(5), CancellationToken.None).AsTask();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await held.DisposeAsync();
        var taken = await waiting;

        Expect.That(taken.IsSuccess, "the waiter gets the released lock");
        await taken.Value.DisposeAsync();
    }
}
