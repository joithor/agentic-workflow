using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxDown;

public sealed class OnyxDownTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OnyxDownService Service(OnyxUpRig rig)
        => new(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Runtime);

    private static async Task<Kernel.Result<OnyxDownResponse>> Down(OnyxUpRig rig)
        => await Service(rig).Handle(new OnyxDownRequest(), Token);

    private static async Task<OnyxUpRig> RunningRig()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_stops_the_project_and_keeps_the_files()
    {
        var rig = await RunningRig();
        var (files, state, stateWrites) = (rig.Workspace.Files.ToDictionary(static f => f.Key, static f => f.Value, StringComparer.Ordinal), rig.Workspace.State, rig.Workspace.StateWrites);

        var response = (await Down(rig)).Value;

        Assert.True(response.WasRunning);
        Assert.Contains("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.All(rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
        Assert.Equal(files.Count, rig.Workspace.Files.Count);
        Assert.All(files, f => Assert.Equal(f.Value, rig.Workspace.Files[f.Key], StringComparer.Ordinal));
        Assert.Same(state, rig.Workspace.State);
        Assert.Equal(stateWrites, rig.Workspace.StateWrites);
        Assert.False(rig.Workspace.LockHeld);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_on_a_stack_that_is_already_stopped_is_ok_and_says_it_was_not_running()
    {
        var rig = await RunningRig();
        _ = (await Down(rig)).Value;

        var again = (await Down(rig)).Value;

        Assert.False(again.WasRunning);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_on_a_box_never_set_up_is_a_no_op()
    {
        var rig = new OnyxUpRig();

        var response = (await Down(rig)).Value;

        Assert.False(response.WasRunning);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
        Assert.DoesNotContain("Ps", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_refuses_a_remote_endpoint_with_010()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-010", error.Code);
        Assert.Contains("tcp://10.0.0.5:2375", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Stop", rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_reports_a_failed_stop_as_011()
    {
        var rig = await RunningRig();
        rig.Runtime.FailNext("Stop", SindriErrors.OnyxComposeFailed.With("docker compose stop failed"));

        var error = (await Down(rig)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_with_docker_not_answering_is_001()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.DaemonDown;

        Assert.Equal("SND-ONYX-001", (await Down(rig)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_while_another_onyx_command_holds_the_lock_is_busy()
    {
        var rig = await RunningRig();
        using var held = rig.Workspace.TryLock();

        Assert.Equal("SND-API-009", (await Down(rig)).Error.Code);
    }
}
