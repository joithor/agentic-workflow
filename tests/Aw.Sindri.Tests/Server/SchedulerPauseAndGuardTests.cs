using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>The scheduler honours pauses and the run guard (API P6 unit D), on the stepped fake clock.</summary>
[Lane(TestLane.Unit)]
public sealed class SchedulerPauseAndGuardTests
{
    private static readonly ServiceName _tidy = ServiceName.From("tidy");

    [Fact]
    public async Task Paused_service_runs_no_slot_for_3_cadences()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        var counted = new CountingService();
        await using var rig = await SchedulerRig.Start([tidy, counted], ("tidy", "5s"), ("counted", "5s"));
        await rig.Pause("tidy");
        await rig.Begin();

        await rig.Step(17);

        Assert.True(counted.Runs >= 3);   // the schedule is running, three cadences passed
        Expect.Equal(0, tidy.Runs);
    }

    [Fact]
    public async Task Resume_runs_the_next_aligned_slot_not_a_burst()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        await rig.Pause("tidy");
        await rig.Begin();
        await rig.Step(17);

        await rig.Resume("tidy");
        await rig.Step(1);

        Assert.True(tidy.Runs <= 1, "a resume replayed missed slots");
        await rig.Step(10);
        var slots = rig.Runs("tidy").Select(r => r.Slot).ToList();
        Assert.NotEmpty(slots);
        Assert.All(slots.Zip(slots.Skip(1)), pair => Expect.Equal(TimeSpan.FromSeconds(5), pair.Second - pair.First));   // aligned, one per cadence
    }

    [Fact]
    public async Task Scheduler_skips_a_slot_while_a_manual_run_is_in_flight()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        Assert.True(rig.Guard.TryClaim(_tidy, out var manual));   // a manual run holds the service
        await rig.Begin();

        await rig.Step(12);
        var during = tidy.Runs;
        manual.Dispose();
        await rig.Step(1);

        Expect.Equal(0, during);
        Assert.True(tidy.Runs <= 1, "a skipped slot was caught up in a burst");
    }

    [Fact]
    public async Task Manual_failure_then_idle_scheduled_run_records_the_ok()
    {
        var tidy = new ScriptedService(ScriptedService.Worked, ScriptedService.Quiet, ScriptedService.Quiet);
        await using var rig = await SchedulerRig.Start([tidy], ("tidy", "5s"));
        await rig.Begin();
        await rig.Step(2);
        Expect.Equal(1, tidy.Runs);
        rig.Guard.Record(_tidy, ServiceOutcome.Failed);   // a manual run failed in between (its own service.ran is in the ledger)

        await rig.Step(6);

        Expect.Equal(2, tidy.Runs);
        Expect.Equal(2, rig.Runs("tidy").Count);   // the idle run is recorded: the last outcome is not ok, so the failure row clears
    }
}
