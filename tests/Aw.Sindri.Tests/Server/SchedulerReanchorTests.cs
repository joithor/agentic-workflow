using Aw.Kernel.Testing;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// A schedule that is (re)built anchors each service on its latest recorded run, so a restart or a re-approval doesn't
/// run it again early (a job-probe on a 24 h cadence ran three times in six minutes). Cadence 5m stands in for 24 h.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SchedulerReanchorTests
{
    [Fact]
    public async Task A_restart_within_the_period_does_not_rerun_a_service()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5m"));
        await rig.Begin();
        await rig.Step(2);
        Expect.Equal(1, tidy.Runs);

        await rig.Step(58);
        await rig.Restart();
        await rig.Step(120);
        Expect.Equal(1, tidy.Runs);

        await rig.Step(130);   // 5 minutes after the first run
        Expect.Equal(2, tidy.Runs);
    }

    [Fact]
    public async Task Two_restarts_in_one_period_run_the_service_once()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5m"));
        await rig.Begin();
        await rig.Step(2);

        await rig.Restart();
        await rig.Step(30);
        await rig.Restart();
        await rig.Step(30);

        Expect.Equal(1, tidy.Runs);
        Expect.Equal(1, rig.Runs("tidy").Count);
    }

    [Fact]
    public async Task A_reapproval_does_not_rerun_a_service()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        var counted = new CountingService();
        await using var rig = await SchedulerRig.Start([tidy, counted], ("tidy", "5m"));
        await rig.Begin();
        await rig.Step(2);

        await rig.Reapprove(("tidy", "5m"), ("counted", "5m"));
        await rig.Step(5);

        Expect.Equal(1, tidy.Runs);
        Expect.Equal(1, counted.Runs);   // the new service runs at once
    }

    [Fact]
    public async Task An_overdue_service_runs_once_at_start()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5m"));
        await rig.Begin();
        await rig.Step(2);

        await rig.Restart(down: TimeSpan.FromMinutes(40));   // the box slept: eight periods passed
        await rig.Step(5);
        Expect.Equal(2, tidy.Runs);   // one catch-up run, not eight

        await rig.Step(120);
        Expect.Equal(2, tidy.Runs);
        await rig.Step(190);
        Expect.Equal(3, tidy.Runs);
    }

    [Fact]
    public async Task A_ledger_that_cannot_be_read_falls_back_to_now()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5m"));
        await rig.Begin();
        await rig.Step(2);
        rig.Reader.Failing = true;

        await rig.Restart();
        await rig.Step(5);

        Expect.Equal(2, tidy.Runs);   // today's behaviour: the schedule is never blocked
        _ = Assert.Single(rig.Log.Lines, l => l.Contains("SND-SCHED-001", StringComparison.Ordinal) && l.Contains("last runs", StringComparison.Ordinal));
    }
}
