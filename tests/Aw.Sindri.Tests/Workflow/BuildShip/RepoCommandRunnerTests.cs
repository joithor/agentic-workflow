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

        Assert.Equal(["typecheck", "unit"], checks.Value.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal(["full"], green.Value.Select(static c => c.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_batch_runs_in_the_worktree_with_the_extra_arguments_and_one_slot_taken_and_released()
    {
        var rig = new StepRig();
        _ = rig.Commands.Answer("a", StepRig.Pass()).Answer("b", StepRig.Fail());
        PlannedCommand[] plan = [new(new BuildCommand("a", ["a", "x"]), ["t1", "t2"]), new(new BuildCommand("b", ["b"], Timeout: "2h"), [])];

        var ran = await rig.Runner.Run("holder", plan, StepRig.Tree, TimeSpan.FromHours(4), Token);

        Assert.Equal([true, false], ran.Value.Select(static r => r.Run.Passed));   // a failing command does not stop the batch
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
        Assert.Equal(["a", "x", "t1", "t2"], rig.Commands.Calls[0].Command.Argv);
        Assert.Equal((TimeSpan.FromHours(4), StepRig.Tree), (rig.Commands.Calls[0].Command.Timeout, rig.Commands.Calls[0].WorkingDirectory));
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

    private static PlannedCommand[] Plan(int count) => [.. Enumerable.Range(0, count).Select(static i => new PlannedCommand(new BuildCommand(Name(i), [Name(i)]), []))];

    private static string Name(int i) => "c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // A command that "runs" for this long: the rig's clock moves, as the wall clock would.
    private static void Takes(StepRig rig, TimeSpan time, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _ = rig.Commands.Answer(Name(i), _ =>
            {
                rig.Clock.Advance(time);
                return StepRig.Pass();
            });
        }
    }

    [Fact]
    public async Task A_batch_longer_than_the_budget_ends_inside_it_with_a_failure_that_names_the_budget()
    {
        var rig = new StepRig();
        Takes(rig, TimeSpan.FromMinutes(15), 4);
        var budget = RepoCommandRunner.BudgetFor(TimeSpan.FromMinutes(50));   // 45 min: three 15 min commands fit, the fourth does not

        var ran = await rig.Runner.Run("h", Plan(4), StepRig.Tree, budget, Token);

        Assert.True(ran.IsError);
        Assert.Contains("budget", ran.Error.Message, StringComparison.Ordinal);
        Assert.Contains("45", ran.Error.Message, StringComparison.Ordinal);
        Assert.Equal(3, rig.Commands.Calls.Count);   // the fourth never started
        Assert.True(rig.Clock.GetUtcNow() - KnownInstants.Epoch <= TimeSpan.FromMinutes(50));
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task The_slot_wait_spends_the_same_budget_and_each_command_gets_only_what_is_left()
    {
        var rig = new StepRig();
        Takes(rig, TimeSpan.FromMinutes(10), 2);
        rig.Slots.OnTake = () => rig.Clock.Advance(TimeSpan.FromMinutes(25));

        var ran = await rig.Runner.Run("h", Plan(2), StepRig.Tree, TimeSpan.FromMinutes(45), Token);

        Assert.True(ran.IsSuccess);
        Assert.Equal([TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(10)], rig.Commands.Calls.Select(static c => c.Command.Timeout));
    }

    [Fact]
    public async Task The_slot_wait_is_never_longer_than_the_budget()
    {
        var rig = new StepRig();

        _ = await rig.Runner.Run("h", Plan(1), StepRig.Tree, TimeSpan.FromMinutes(10), Token);

        Assert.Equal([TimeSpan.FromMinutes(10)], rig.Slots.Waits);
    }

    [Fact]
    public async Task A_budget_spent_waiting_for_the_slot_fails_before_any_command_starts()
    {
        var rig = new StepRig();
        rig.Slots.OnTake = () => rig.Clock.Advance(TimeSpan.FromMinutes(30));

        var ran = await rig.Runner.Run("h", Plan(1), StepRig.Tree, TimeSpan.FromMinutes(30), Token);

        Assert.True(ran.IsError);
        Assert.Contains("budget", ran.Error.Message, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task A_command_cut_short_by_the_budget_is_the_budget_failure_not_the_commands_own_timeout()
    {
        var rig = new StepRig();
        _ = rig.Commands.Answer("c0", _ =>
        {
            rig.Clock.Advance(TimeSpan.FromMinutes(20));
            return CheckRun.Of(-1, timedOut: true, TimeSpan.FromMinutes(20), string.Empty);
        });

        var ran = await rig.Runner.Run("h", Plan(1), StepRig.Tree, TimeSpan.FromMinutes(20), Token);

        Assert.True(ran.IsError);
        Assert.Contains("budget", ran.Error.Message, StringComparison.Ordinal);
        Assert.Contains("c0", ran.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_command_that_ran_past_its_own_shorter_timeout_stays_an_answer()
    {
        var rig = new StepRig();
        _ = rig.Commands.Answer("c0", CheckRun.Of(-1, timedOut: true, TimeSpan.FromMinutes(1), string.Empty));
        PlannedCommand[] plan = [new(new BuildCommand("c0", ["c0"], Timeout: "1m"), [])];

        var ran = await rig.Runner.Run("h", plan, StepRig.Tree, TimeSpan.FromMinutes(20), Token);

        Assert.True(ran.Value.Single().Run.TimedOut);
    }
}
