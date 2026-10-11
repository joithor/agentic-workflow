using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Infrastructure;
using Aw.Evidence.Infrastructure.Locks;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

[Lane(TestLane.Integration)]
public sealed class DirectoryLocksTests : IDisposable
{
    private readonly TempDirectory _root = new();

    [Fact]
    public async Task A_held_lock_is_a_directory_with_an_owner_line_and_release_removes_it()
    {
        var settings = LockTestEnvironment.Settings(_root);
        var lease = (await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var owner = await File.ReadAllTextAsync(Path.Combine(settings.QaStack, "owner"), TestContext.Current.CancellationToken);
        Assert.Matches($"^{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)} \\d{{4}}-\\d\\d-\\d\\dT\\d\\d:\\d\\d:\\d\\dZ ui-evidence\n$", owner);

        await lease.DisposeAsync();
        Assert.False(Directory.Exists(settings.QaStack));
        Assert.True(Directory.Exists(Path.GetDirectoryName(settings.QaStack)), "the locks directory stays");
    }

    [Fact]
    public async Task Release_leaves_a_slot_another_holder_took_after_ours_was_removed_by_hand()
    {
        var settings = LockTestEnvironment.Settings(_root);
        var lease = (await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;
        Directory.Delete(settings.QaStack, recursive: true);   // a person "clears a stuck lock" that wasn't stuck
        _ = Directory.CreateDirectory(settings.QaStack);
        await File.WriteAllTextAsync(Path.Combine(settings.QaStack, "owner"), "1 2026-10-10T12:00:00Z heavy\n", TestContext.Current.CancellationToken);

        await lease.DisposeAsync();

        Assert.True(File.Exists(Path.Combine(settings.QaStack, "owner")), "the other holder keeps its lock");
    }

    [Fact]
    public async Task A_waiter_says_once_which_lock_it_waits_on()
    {
        var notices = new StringWriter();
        await using var held = (await LockTestEnvironment.Locks(_root, slots: "1").AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var busy = await LockTestEnvironment.Locks(_root, notices: notices, slots: "1").AcquireAsync(LockKind.HeavyJob, TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
        Assert.Equal($"ui-evidence: waiting on lock: {LockTestEnvironment.Settings(_root).HeavyJob}{Environment.NewLine}", notices.ToString());
    }

    [Fact]
    public async Task With_the_heavy_lock_held_by_a_parent_the_heavy_job_lease_is_a_no_op()
    {
        var settings = LockTestEnvironment.Settings(_root, heavyHeld: true);
        await using var first = (await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var second = await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.True(second.IsSuccess);
        Assert.Equal([LockKind.HeavyJob], second.Value.Held);
        Assert.False(Directory.Exists(settings.HeavyJob), "an inherited lease creates nothing");
        await second.Value.DisposeAsync();
    }

    [Fact]
    public async Task The_heavy_flag_does_not_cover_the_qa_stack_lock()
    {
        await using var held = (await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var busy = await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
    }

    [Fact]
    public async Task Two_holders_take_both_heavy_slots_and_a_third_times_out_busy()
    {
        var notices = new StringWriter();
        var first = (await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;
        var second = (await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        Assert.True(File.Exists(Path.Combine(LockTestEnvironment.HeavySlot(_root, 1), "owner")), "slot 1 is the lock path");
        Assert.True(File.Exists(Path.Combine(LockTestEnvironment.HeavySlot(_root, 2), "owner")), "slot 2 is <lock>.2");
        var third = await LockTestEnvironment.Locks(_root, notices: notices).AcquireAsync(LockKind.HeavyJob, TimeSpan.FromSeconds(1.2), TestContext.Current.CancellationToken);
        Assert.Equal(EvidenceErrors.LockBusy.Code, third.Error.Code);
        Assert.Contains("2 slots", third.Error.Message, StringComparison.Ordinal);

        await first.DisposeAsync();
        await second.DisposeAsync();
        Assert.Empty(Directory.GetFileSystemEntries(Path.GetDirectoryName(LockTestEnvironment.HeavySlot(_root, 1))!));   // why: the lock path always has a parent
    }

    [Fact]
    public async Task With_one_slot_the_heavy_job_lock_is_a_single_mutex()
    {
        await using var held = (await LockTestEnvironment.Locks(_root, slots: "1").AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var busy = await LockTestEnvironment.Locks(_root, slots: "1").AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
        Assert.False(Directory.Exists(LockTestEnvironment.HeavySlot(_root, 2)));
    }

    [Fact]
    public async Task An_invalid_slot_count_is_reported_once_and_two_slots_are_used()
    {
        var notices = new StringWriter();
        var locks = LockTestEnvironment.Locks(_root, notices: notices, slots: "zero");
        await using var first = (await locks.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;
        await using var second = (await locks.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        Assert.Equal($"ui-evidence: AW_HEAVY_SLOTS='zero' isn't a whole number of at least 1; using 2 slots{Environment.NewLine}", notices.ToString());
        Assert.True(Directory.Exists(LockTestEnvironment.HeavySlot(_root, 2)));
    }

    [Fact]
    public async Task The_qa_stack_lock_stays_one_slot_whatever_the_heavy_slot_count()
    {
        await using var held = (await LockTestEnvironment.Locks(_root, slots: "4").AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var busy = await LockTestEnvironment.Locks(_root, slots: "4").AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
        Assert.False(Directory.Exists(LockTestEnvironment.Settings(_root).QaStack + ".2"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task A_lock_path_with_trailing_separators_is_taken_and_released_and_slot_two_sits_beside_it(int separators)
    {
        var trailing = new string(Path.DirectorySeparatorChar, separators);
        var qa = Path.Combine(_root.Path, "locks", "qa.lock");
        var heavy = Path.Combine(_root.Path, "locks", "h.lock");
        var settings = SettingsFromEnvironment.Read(name => name switch
        {
            "AW_QA_STACK_LOCK" => qa + trailing,
            "AW_HEAVY_JOB_LOCK" => heavy + trailing,
            "AW_HEAVY_LOCK_HELD" => "0",
            "AW_HEAVY_SLOTS" => "2",
            _ => null,
        }).Locks;
        var locks = new DirectoryLocks(settings, TimeProvider.System, TextWriter.Null);

        var stack = (await locks.AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;
        var first = (await locks.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;
        var second = (await locks.AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        Assert.True(File.Exists(Path.Combine(qa, "owner")), "the qa-stack lock is the path itself");
        Assert.True(File.Exists(Path.Combine(heavy + ".2", "owner")), "slot 2 is <lock>.2 beside slot 1");
        await stack.DisposeAsync();
        await first.DisposeAsync();
        await second.DisposeAsync();
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(_root.Path, "locks")));
    }

    public void Dispose() => _root.Dispose();
}
