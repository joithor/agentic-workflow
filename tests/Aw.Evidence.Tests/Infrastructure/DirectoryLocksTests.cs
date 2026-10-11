using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Locks;
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

        var owner = await File.ReadAllTextAsync(Path.Combine(settings.QaStackLock, "owner"), TestContext.Current.CancellationToken);
        Assert.Matches($"^{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)} \\d{{4}}-\\d\\d-\\d\\dT\\d\\d:\\d\\d:\\d\\dZ ui-evidence\n$", owner);

        await lease.DisposeAsync();
        Assert.False(Directory.Exists(settings.QaStackLock));
        Assert.True(Directory.Exists(Path.GetDirectoryName(settings.QaStackLock)), "the locks directory stays");
    }

    [Fact]
    public async Task A_waiter_says_once_which_lock_it_waits_on()
    {
        var notices = new StringWriter();
        await using var held = (await LockTestEnvironment.Locks(_root).AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var busy = await LockTestEnvironment.Locks(_root, notices: notices).AcquireAsync(LockKind.HeavyJob, TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
        Assert.Equal($"ui-evidence: waiting on lock: {LockTestEnvironment.Settings(_root).HeavyJobLock}{Environment.NewLine}", notices.ToString());
    }

    [Fact]
    public async Task With_the_heavy_lock_held_by_a_parent_the_heavy_job_lease_is_a_no_op()
    {
        var settings = LockTestEnvironment.Settings(_root, heavyHeld: true);
        await using var first = (await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var second = await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.HeavyJob, TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.True(second.IsSuccess);
        Assert.Equal([LockKind.HeavyJob], second.Value.Held);
        Assert.False(Directory.Exists(settings.HeavyJobLock), "an inherited lease creates nothing");
        await second.Value.DisposeAsync();
    }

    [Fact]
    public async Task The_heavy_flag_does_not_cover_the_qa_stack_lock()
    {
        await using var held = (await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken)).Value;

        var busy = await LockTestEnvironment.Locks(_root, heavyHeld: true).AcquireAsync(LockKind.QaStack, TimeSpan.Zero, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.LockBusy.Code, busy.Error.Code);
    }

    public void Dispose() => _root.Dispose();
}
