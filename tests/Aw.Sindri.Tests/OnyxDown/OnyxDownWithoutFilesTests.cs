using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxDown;

// With the compose file there but Sindri's override or .env gone, compose can't read the project's files; the containers are still stopped by the project label.
public sealed class OnyxDownWithoutFilesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Kernel.Result<OnyxDownResponse>> Down(OnyxUpRig rig)
        => await new OnyxDownService(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Runtime).Handle(new OnyxDownRequest(), Token);

    private static async Task<OnyxUpRig> RunningRigWithout(Func<OnyxPaths, string> file)
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        Assert.True(rig.Workspace.Files.Remove(file(rig.Paths)));
        rig.Runtime.Calls.Clear();
        return rig;
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("override")]
    [InlineData("env")]
    public async Task Down_stops_the_live_containers_by_project_label_when_a_rendered_file_is_missing(string missing)
    {
        var rig = await RunningRigWithout(missing == "override" ? static p => p.Override : static p => p.Env);

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.Contains("nginx", response.Stopped, StringComparer.Ordinal);
        Assert.Equal(["Ps", "Stop"], rig.Runtime.LabelOnlyCalls);
        Assert.Contains(response.Warnings, static w => w.Contains("project label", StringComparison.Ordinal));
        Assert.All(rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_the_files_present_still_uses_them()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;

        _ = (await Down(rig)).Value;

        Assert.Empty(rig.Runtime.LabelOnlyCalls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_whose_label_stop_fails_is_011_with_the_down_fix()
    {
        var rig = await RunningRigWithout(static p => p.Override);
        rig.Runtime.FailNext("Stop", SindriErrors.OnyxComposeFailed.With("`docker compose stop` failed (exit 1): boom"));

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Contains("run `sindri onyx down` again", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri onyx up", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_down_never_says_to_run_up()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        rig.Runtime.FailNext("Stop", SindriErrors.OnyxComposeFailed.With("`docker compose stop` failed (exit 1): boom"));

        var error = (await Down(rig)).Error;

        Assert.Contains("run `sindri onyx down` again", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri onyx up", error.Message, StringComparison.Ordinal);
    }
}
