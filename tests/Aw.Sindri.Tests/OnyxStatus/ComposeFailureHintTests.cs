using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

// What to do next after an SND-ONYX-011 comes from the command that failed, not from the docker adapter: up, down, status and doctor each know their own fix.
public sealed class ComposeFailureHintTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ToolRun Failed => new(ToolOutcome.Exited, 1, "");

    [Fact, Trait("Lane", "unit")]
    public void A_failed_compose_command_says_what_it_printed_and_leaves_the_next_step_to_the_caller()
    {
        var cli = new DockerCliRuntime(new FakeToolRunner(Failed, ["boom"]), static _ => null);

        var error = cli.Stop(OnyxPaths.For(OnyxUpRig.Home).Project, Token).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Contains("boom", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri onyx up", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_project_without_files_is_addressed_by_its_name_alone()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 0, ""));

        _ = new DockerCliRuntime(runner, static _ => null).Stop(ComposeProject.ByLabel("sindri-onyx-0a1b2c3d"), Token);

        Assert.Equal(["compose", "-p", "sindri-onyx-0a1b2c3d", "stop"], Assert.Single(runner.Calls).Arguments, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_up_step_says_to_run_up_again_to_resume()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.FailNext("Pull", SindriErrors.OnyxComposeFailed.With("`docker compose pull` failed (exit 1): no space left on device"));

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.EndsWith("run `sindri onyx up` again to resume", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_names_the_repair_of_onyx_files_when_the_stack_cannot_be_read()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.FailNext("Ps", SindriErrors.OnyxComposeFailed.With("`docker compose ps` failed (exit 1): no configuration file provided"));

        var check = await new OnyxStackDoctor(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Pins).Check(Token);

        Assert.Equal(CheckState.Warn, check!.State);
        Assert.Contains("run `sindri onyx up` to repair Onyx's files", check.Fix, StringComparison.Ordinal);
    }
}
