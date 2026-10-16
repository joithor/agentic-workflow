using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>A stop and a lost lease end the jobs the process runs (API P6 unit F): their tokens trip, and the job writes no end of its own (recovery at the next start does).</summary>
[Lane(TestLane.Unit)]
public sealed class JobStopTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static bool Wrote(RealJobRuntime runtime, string type) => runtime.Events.Any(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal));

    [Fact]
    public async Task Stop_cancels_running_jobs()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();
        _ = await runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), A.Caller().Human().Build());

        runtime.Shutdown.Request();
        await runtime.Quiesce();

        Expect.Equal(1, runtime.Probe.Cancelled);
        Assert.False(Wrote(runtime, EventTypes.JobFinished));
        Assert.False(Wrote(runtime, EventTypes.JobCancelled));
    }

    [Fact]
    public async Task Lease_lost_cancels_jobs()
    {
        await using var runtime = new RealJobRuntime();
        var grant = await runtime.H.HoldLease();
        _ = await runtime.Start(new EchoJobRequest(Lines: 100, DelayMs: 100), A.Caller().Human().Build());
        _ = await runtime.H.Store.Release(grant, Token);   // the row no longer names this holder: renewing is SND-LEASE-002
        var clock = new SteppedClock(runtime.H.Clock);
        using var renewer = new LeaseRenewer(new ScriptedLease(runtime.H.Store), runtime.H.Held, runtime.Shutdown, new FakeExitCode(), clock, new ListLogger<LeaseRenewer>());
        await renewer.StartAsync(Token);

        await clock.Step(LeaseTiming.RenewEvery);
        await clock.Armed(() => runtime.Shutdown.Requested);
        await runtime.Quiesce();

        Assert.True(runtime.Shutdown.Requested);
        Expect.Equal(1, runtime.Probe.Cancelled);
        Assert.False(Wrote(runtime, EventTypes.JobFinished));
    }
}
