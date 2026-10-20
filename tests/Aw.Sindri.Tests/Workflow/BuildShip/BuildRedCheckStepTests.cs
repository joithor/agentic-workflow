using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Build;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>'step.build.red-check' on the block runtime (line spec §7.2, BLD-RED-001): each item's red tests must fail on the red commit.</summary>
[Lane(TestLane.Unit)]
public sealed class BuildRedCheckStepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StepContext Context(string redTests = """{"commit":"h1","items":{"A1":{"tests":["T1","T2"]},"A2":{"tests":["T3"]},"A3":{"appOnly":"the page shows it"}}}""") =>
        StepRig.Context("step.build.red-check", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["activePacket"] = $$"""{"hash":"sha256:x","dir":"{{StepRig.PacketDir}}","repo":"{{StepRig.Tree}}"}""",
            ["redTests"] = redTests,
        });

    private static BuildRedCheckStep Step(StepRig rig) => new(rig.Diff, rig.Runner);

    [Fact]
    public async Task Tests_that_all_fail_report_red_the_red_command_runs_per_item_with_the_ids_in_the_worktree_under_one_slot()
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red), StepRig.Cmd("typecheck", BuildCommand.Checks));
        _ = rig.Commands.Answer("red-tests", StepRig.Fail());

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("red", result.Result!.Outcome.Value);
        Assert.Equal([["red-tests", "run", "T1", "T2"], ["red-tests", "run", "T3"]], rig.Commands.Calls.Select(static c => c.Command.Argv.ToArray()));   // the appOnly item is not run
        Assert.All(rig.Commands.Calls, static c => Assert.Equal(StepRig.Tree, c.WorkingDirectory));
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task An_item_whose_tests_pass_is_vacuous_and_a_finding_names_it_and_its_tests()
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red));
        _ = rig.Commands.Answer("red-tests", _ => rig.Commands.Calls.Count == 1 ? StepRig.Fail() : StepRig.Pass("3 passed"));

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("vacuous", result.Result!.Outcome.Value);
        var finding = StepRig.Out(result, "findings").AsArray().Single()!;
        Assert.Equal("BLD-RED-001", (string?)finding["rule"]);
        Assert.Contains("A2", (string?)finding["message"], StringComparison.Ordinal);
        Assert.Contains("T3", (string?)finding["message"], StringComparison.Ordinal);
        Assert.Equal("3 passed", (string?)finding["detail"]);
    }

    [Fact]
    public async Task A_command_that_runs_out_of_time_fails_the_Step_instead_of_judging_the_tests()
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red));
        _ = rig.Commands.Answer("red-tests", CheckRun.Of(-1, timedOut: true, TimeSpan.FromMinutes(40), string.Empty));

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("ran past its timeout", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_worktree_that_moved_past_the_red_commit_fails_the_Step_and_runs_nothing()
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red));
        rig.Diff.Advance(StepRig.Tree);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("not the red commit h1", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }

    [Fact]
    public async Task A_profile_with_no_red_command_fails_the_Step_with_the_fix_and_takes_no_slot()
    {
        var rig = new StepRig(StepRig.Cmd("typecheck", BuildCommand.Checks));

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("on: red", result.Failure, StringComparison.Ordinal);
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"items":{"A1":{"appOnly":"s"}}}""")]
    public async Task No_test_to_run_fails_the_Step(string redTests)
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red));

        var result = await Step(rig).Run(Context(redTests), Token);

        Assert.Contains("no test to run", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_active_packet_repo_fails_the_Step()
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red));

        var result = await Step(rig).Run(StepRig.Context("step.build.red-check", new Dictionary<string, string>(StringComparer.Ordinal) { ["activePacket"] = """{"hash":"h","dir":"/d"}""", ["redTests"] = "{}" }), Token);

        Assert.Contains("fact:activePacket", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_dirty_worktree_fails_the_Step_and_runs_nothing()
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red));
        rig.Diff.Touch(StepRig.Tree);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Contains("uncommitted", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Fact]
    public async Task Red_tests_without_the_red_commit_fail_the_Step_and_run_nothing()
    {
        var rig = new StepRig(StepRig.Cmd("red-tests", BuildCommand.Red));

        var result = await Step(rig).Run(Context("""{"items":{"A1":{"tests":["T1"]}}}"""), Token);

        Assert.Contains("fact:redTests.commit", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
    }
}
