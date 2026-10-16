using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>The recovery host (API P6 unit F): it tries each second until the views have caught up, then ends the job once and stops.</summary>
[Lane(TestLane.Unit)]
public sealed class JobRecoveryHostTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task It_retries_until_the_views_catch_up_then_ends_the_job_and_finishes()
    {
        var rig = new RecoveryRig();
        _ = await rig.H.HoldLease();
        _ = await rig.Left(Testing.A.FirstJobId);
        rig.Progress.Publish(new ViewsStatus(Cursor: 0, LedgerHead: 1, ProjectedAt: null, Rebuilding: false, Stalled: true, StalledAt: 1, StalledType: "job.started"));
        var clock = new SteppedClock(rig.H.Clock);
        using var host = new JobRecoveryHost(rig.Recovery, clock, new ListLogger<JobRecoveryHost>());
        await host.StartAsync(Token);

        await clock.Step(TimeSpan.FromSeconds(1));   // the first attempt found the views stalled; it waits a second
        await clock.Armed();
        Assert.Empty(rig.Of(EventTypes.JobFinished));
        rig.Caught();
        await clock.Step(TimeSpan.FromSeconds(1));
        await host.ExecuteTask!.WaitAsync(Token);   // why: StartAsync set it

        _ = Assert.Single(rig.Of(EventTypes.JobFinished));
    }
}
