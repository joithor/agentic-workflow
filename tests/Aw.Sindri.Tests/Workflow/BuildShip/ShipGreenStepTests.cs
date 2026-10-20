using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Ship;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>'step.ship.green' on the block runtime (line spec §7.5, SHIP-SECRET-001): the secret scan, then the profile's green commands under one heavy slot.</summary>
[Lane(TestLane.Unit)]
public sealed class ShipGreenStepTests
{
    private const string Secret = "hunter2-not-a-real-credential";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static StepContext Context() => StepRig.Context("step.ship.green", new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["workspace"] = $"\"{StepRig.Tree}\"",
        ["phasePacket"] = $$"""{"hash":"sha256:x","dir":"{{StepRig.PacketDir}}"}""",
    });

    private static ShipGreenStep Step(StepRig rig) => new(rig.Diff, rig.Changes, rig.Files, rig.Runner, new Mask());

    [Fact]
    public async Task Green_commands_that_pass_report_green_with_the_head_the_base_and_each_commands_exit()
    {
        var rig = new StepRig(StepRig.Cmd("full", BuildCommand.Green), StepRig.Cmd("lint", BuildCommand.Green), StepRig.Cmd("typecheck", BuildCommand.Checks)).Branch([new ChangedFile("src/a.cs", ChangeKind.Added)]);
        _ = rig.Commands.Answer("full", StepRig.Pass()).Answer("lint", StepRig.Pass());

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("green", result.Result!.Outcome.Value);
        var green = StepRig.Out(result, "green");
        Assert.Equal(("h1", "b0"), ((string?)green["head"], (string?)green["base"]));
        Assert.Equal(["full", "lint"], green["commands"]!.AsArray().Select(static c => (string?)c!["name"]), StringComparer.Ordinal);
        Assert.Equal((1, 1), (rig.Slots.Taken, rig.Slots.Released));
    }

    [Fact]
    public async Task A_secret_on_an_added_line_is_red_names_the_file_and_line_and_runs_nothing()
    {
        var rig = new StepRig(StepRig.Cmd("full", BuildCommand.Green)).Branch([new ChangedFile("src/a.cs", ChangeKind.Added)], [new AddedLine("src/a.cs", 3, Secret)]);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("red", result.Result!.Outcome.Value);
        var finding = StepRig.Out(result, "findings").AsArray().Single()!;
        Assert.Equal(("SHIP-SECRET-001", "src/a.cs", 3), ((string?)finding["rule"], (string?)finding["path"], (int?)finding["line"]));
        Assert.DoesNotContain(Secret, finding.ToJsonString(), StringComparison.Ordinal);
        Assert.Empty(rig.Commands.Calls);
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Fact]
    public async Task A_failing_or_timed_out_command_is_red_with_its_tail()
    {
        var rig = new StepRig(StepRig.Cmd("full", BuildCommand.Green), StepRig.Cmd("lint", BuildCommand.Green)).Branch([]);
        _ = rig.Commands.Answer("full", StepRig.Fail("2 failed")).Answer("lint", CheckRun.Of(-1, timedOut: true, TimeSpan.FromMinutes(90), string.Empty));

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("red", result.Result!.Outcome.Value);
        var findings = StepRig.Out(result, "findings").AsArray();
        Assert.Equal("full exited 1", (string?)findings[0]!["message"]);
        Assert.Equal("2 failed", (string?)findings[0]!["detail"]);
        Assert.Equal("lint ran past its timeout", (string?)findings[1]!["message"]);
    }

    [Fact]
    public async Task A_profile_with_no_green_command_greens_with_an_empty_command_list_so_nothing_ran_is_visible()
    {
        var rig = new StepRig().Branch([]);

        var result = await Step(rig).Run(Context(), Token);

        Assert.Equal("green", result.Result!.Outcome.Value);
        Assert.Empty(StepRig.Out(result, "green")["commands"]!.AsArray());
        Assert.Equal(0, rig.Slots.Taken);
    }

    [Fact]
    public async Task A_missing_workspace_or_packet_fails_the_Step()
    {
        var rig = new StepRig().Branch([]);

        var result = await Step(rig).Run(StepRig.Context("step.ship.green", new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace"] = "\"/work/p1\"" }), Token);

        Assert.Contains("fact:phasePacket", result.Failure, StringComparison.Ordinal);
    }

    private sealed class Mask : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(Secret, "[secret]", StringComparison.Ordinal);
    }
}
