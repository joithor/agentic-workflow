using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>The repo's commands for a Step (line spec LN-WF9): picked by stage from the approved profile, and run under one heavy slot that is released on every path.</summary>
[Lane(TestLane.Unit)]
public sealed class RepoCommandRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_stage_gets_its_commands_in_the_profiles_order_and_the_default_stage_is_checks()
    {
        var rig = new StepRig(StepRig.Cmd("typecheck", BuildCommand.Checks), new BuildCommand("unit", ["u"]), StepRig.Cmd("red-tests", BuildCommand.Red), StepRig.Cmd("full", BuildCommand.Green));

        var checks = await rig.Runner.For(BuildCommand.Checks, Token);
        var green = await rig.Runner.For(BuildCommand.Green, Token);

        Assert.Equal(["typecheck", "unit"], checks.Value.Select(static c => c.Name));
        Assert.Equal(["full"], green.Value.Select(static c => c.Name));
    }

    [Fact]
    public async Task A_batch_runs_in_the_worktree_with_the_extra_arguments_and_one_slot_taken_and_released()
    {
        var rig = new StepRig();
        _ = rig.Commands.Answer("a", StepRig.Pass()).Answer("b", StepRig.Fail());
        PlannedCommand[] plan = [new(new BuildCommand("a", ["a", "x"]), ["t1", "t2"]), new(new BuildCommand("b", ["b"], Timeout: "2h"), [])];

        var ran = await rig.Runner.Run("holder", plan, StepRig.Tree, TimeSpan.FromMinutes(40), Token);

        Assert.Equal([true, false], ran.Value.Select(static r => r.Run.Passed));   // a failing command does not stop the batch
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
        Assert.Equal(["a", "x", "t1", "t2"], rig.Commands.Calls[0].Command.Argv);
        Assert.Equal((TimeSpan.FromMinutes(40), StepRig.Tree), (rig.Commands.Calls[0].Command.Timeout, rig.Commands.Calls[0].WorkingDirectory));
        Assert.Equal(TimeSpan.FromHours(2), rig.Commands.Calls[1].Command.Timeout);
    }

    [Fact]
    public async Task The_slot_is_released_when_a_command_throws()
    {
        var rig = new StepRig();
        _ = rig.Commands.Answer("a", _ => throw new OperationCanceledException());

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () => await rig.Runner.Run("h", [new PlannedCommand(new BuildCommand("a", ["a"]), [])], StepRig.Tree, TimeSpan.FromMinutes(1), Token));

        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task A_busy_lock_is_an_error_and_nothing_runs()
    {
        var rig = new StepRig();
        rig.Slots.Busy = true;

        var ran = await rig.Runner.Run("h", [new PlannedCommand(new BuildCommand("a", ["a"]), [])], StepRig.Tree, TimeSpan.FromMinutes(1), Token);

        Assert.True(ran.IsError);
        Assert.Contains("held by someone", ran.Error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }

    [Fact]
    public async Task An_empty_plan_takes_no_slot()
    {
        var rig = new StepRig();

        var ran = await rig.Runner.Run("h", [], StepRig.Tree, TimeSpan.FromMinutes(1), Token);

        Assert.Empty(ran.Value);
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Fact]
    public async Task A_command_that_cannot_start_is_an_error_and_the_slot_is_still_released()
    {
        var rig = new StepRig();

        var ran = await rig.Runner.Run("h", [new PlannedCommand(new BuildCommand("missing", ["missing"]), [])], StepRig.Tree, TimeSpan.FromMinutes(1), Token);

        Assert.True(ran.IsError);
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }
}
