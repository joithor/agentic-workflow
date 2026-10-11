using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IMachineLocks contract: the QA-stack lock has one holder, the heavy-job lock one holder per slot, kinds are
/// independent, and a release lets the next holder in.
/// </summary>
public abstract class MachineLocksContract
{
    /// <summary>How many holders the heavy-job lock admits at once in the locks <see cref="CreatePair"/> returns.</summary>
    protected abstract int HeavyJobSlots { get; }

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
        await using var held = (await first.AcquireAsync(LockKind.QaStack, TimeSpan.Zero, CancellationToken.None)).Value;

        var busy = await second.AcquireAsync(LockKind.QaStack, TimeSpan.Zero, CancellationToken.None);

        Expect.That(busy.IsError && busy.Error.Code == EvidenceErrors.LockBusy.Code, "a held lock is AW-EVIDENCE-009 for another holder");
    }

    /// <summary>The heavy-job lock admits one holder per slot; the next one is busy.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_heavy_job_lock_admits_one_holder_per_slot_and_no_more()
    {
        var (first, second) = CreatePair();
        var leases = new List<ILockLease>();
        for (var slot = 0; slot < HeavyJobSlots; slot++)
        {
            var lease = await first.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, CancellationToken.None);
            Expect.That(lease.IsSuccess, string.Create(CultureInfo.InvariantCulture, $"slot {slot + 1} of {HeavyJobSlots} is taken"));
            leases.Add(lease.Value);
        }

        var busy = await second.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, CancellationToken.None);

        Expect.That(busy.IsError && busy.Error.Code == EvidenceErrors.LockBusy.Code, "with every slot held the heavy-job lock is AW-EVIDENCE-009");
        foreach (var lease in leases)
        {
            await lease.DisposeAsync();
        }
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
    /// <param name="kind">The lock.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(LockKind.QaStack)]
    [InlineData(LockKind.HeavyJob)]
    public async Task A_waiter_gets_the_lock_when_it_is_released(LockKind kind)
    {
        var (first, second) = CreatePair();
        var held = new List<ILockLease>();
        for (var slot = 0; slot < (kind == LockKind.HeavyJob ? HeavyJobSlots : 1); slot++)
        {
            held.Add((await first.AcquireAsync(kind, TimeSpan.Zero, CancellationToken.None)).Value);
        }

        var waiting = second.AcquireAsync(kind, TimeSpan.FromSeconds(5), CancellationToken.None).AsTask();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await held[^1].DisposeAsync();
        var taken = await waiting;

        Expect.That(taken.IsSuccess, "the waiter gets the released lock");
        await taken.Value.DisposeAsync();
        foreach (var lease in held)
        {
            await lease.DisposeAsync();
        }
    }
}
