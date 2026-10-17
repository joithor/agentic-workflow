using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class OnyxStatusTests
{
    private const long Gib = 1L << 30;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OnyxStatusService Service(OnyxUpRig rig)
        => new(rig.Pins, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Health, rig.Workspace);

    private static async Task<OnyxStatusResponse> Status(OnyxUpRig rig)
        => (await Service(rig).Handle(new OnyxStatusRequest(), Token)).Value;

    private static async Task<OnyxStatusResponse> Status(long freeBytes)
    {
        var rig = new OnyxUpRig();
        rig.Probes.FreeBytes = freeBytes;
        return await Status(rig);
    }

    private static async Task<OnyxStatusResponse> StatusWithDataRoot(bool onThisHost)
    {
        var rig = new OnyxUpRig();
        rig.Probes.FreeBytes = 100 * Gib;
        if (onThisHost)
        {
            _ = rig.Probes.Directories.Add("/var/lib/docker");
            rig.Probes.VolumeFreeBytes["/var/lib/docker"] = 12 * Gib;
        }

        return await Status(rig);
    }

    private static async Task<OnyxUpRig> RunningRig()
    {
        var rig = new OnyxUpRig();
        _ = (await rig.Up(Token)).Value;
        return rig;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_with_an_unreadable_fetched_file_reports_SND_ONYX_012_instead_of_calling_it_not_set_up()
    {
        var rig = await RunningRig();
        _ = rig.Workspace.Unreadable.Add(rig.Paths.Compose);

        var result = await Service(rig).Handle(new OnyxStatusRequest(), Token);

        Assert.Equal("SND-ONYX-012", result.IsError ? result.Error.Code : "no error: status said " + result.Value.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_measures_a_data_root_that_is_a_directory_on_this_host()
    {
        var status = await StatusWithDataRoot(onThisHost: true);

        var disk = Assert.Single(status.Checks, static c => c.Name == "disk");
        Assert.Equal(CheckState.Fail, disk.State);
        Assert.Equal(1, status.ExitCode);
        Assert.StartsWith("12.0 GiB free at /var/lib/docker", disk.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_keeps_the_home_measurement_and_notes_the_vm_cap_when_the_data_root_is_in_a_vm()
    {
        var status = await StatusWithDataRoot(onThisHost: false);

        var disk = Assert.Single(status.Checks, static c => c.Name == "disk");
        Assert.Equal(CheckState.Ok, disk.State);
        Assert.Equal("100.0 GiB free at /sindri-home, needs 16.3 GiB; Docker's data lives in a VM here; its disk image size is capped separately (Docker Desktop: Settings → Resources)", disk.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_not_set_up_with_the_pins_version()
    {
        var status = await Status(100 * Gib);

        Assert.Equal(OnyxState.NotSetUp, status.State);
        Assert.Equal("v4.9.0", status.PinnedVersion);
        Assert.Equal(40, status.PinnedCommit.Length);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(3000, status.Port);
        Assert.Equal(["docker", "compose", "memory", "disk", "port"], status.Checks.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.All(status.Checks, static c => Assert.Equal(CheckState.Ok, c.State));
        Assert.Null(status.Url);
        Assert.Null(status.RunningVersion);
        Assert.Contains("sindri onyx up", status.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_exit_1_when_disk_is_short()
    {
        var status = await Status(11 * Gib);

        Assert.Equal(1, status.ExitCode);
        Assert.Equal("SND-ONYX-003", status.Checks.Single(static c => c.Name == "disk").Problem?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_running_with_url_and_version_when_nginx_and_api_server_run()
    {
        var rig = await RunningRig();

        var status = await Status(rig);

        Assert.Equal(OnyxState.Running, status.State);
        Assert.Equal("http://127.0.0.1:3000", status.Url);
        Assert.Equal("v4.9.0", status.RunningVersion);
        Assert.Equal(3000, status.Port);
        Assert.Equal(0, status.ExitCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_takes_the_port_nginx_publishes_over_the_one_state_json_recorded()
    {
        var rig = await RunningRig();
        rig.Workspace.State = rig.Workspace.State! with { Port = 3005 };
        var nginx = rig.Runtime.Containers.Single(static c => c.Service == "nginx");
        rig.Runtime.Containers[rig.Runtime.Containers.IndexOf(nginx)] = nginx with { Publishers = [new ComposePublisher("127.0.0.1", 3001, 80)] };

        var status = await Status(rig);

        Assert.Equal(OnyxState.Running, status.State);
        Assert.Equal(3001, status.Port);
        Assert.Equal("http://127.0.0.1:3001", status.Url);
        Assert.Equal([3001], rig.Health.Probed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_says_unreachable_when_the_running_stack_does_not_answer()
    {
        var rig = await RunningRig();
        rig.Health.Default = new OnyxProbe(Healthy: false, BackendVersion: null);

        var status = await Status(rig);

        Assert.Equal(OnyxState.Running, status.State);
        Assert.Equal("unreachable", status.RunningVersion);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_stopped_after_down()
    {
        var rig = await RunningRig();
        _ = rig.Runtime.Stop(rig.Paths.Project, Token);

        var status = await Status(rig);

        Assert.Equal(OnyxState.Stopped, status.State);
        Assert.Null(status.Url);
        Assert.Contains("sindri onyx up", status.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_not_set_up_without_a_done_start_step()
    {
        var rig = await RunningRig();
        rig.Workspace.WriteState(new OnyxSetupState(3000, new Dictionary<string, string>(StringComparer.Ordinal) { [OnyxSteps.Fetch] = "x" }));

        Assert.Equal(OnyxState.NotSetUp, (await Status(rig)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_not_set_up_when_the_compose_file_is_gone()
    {
        var rig = await RunningRig();
        _ = rig.Workspace.Files.Remove(rig.Paths.Compose);

        Assert.Equal(OnyxState.NotSetUp, (await Status(rig)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_shows_the_own_port_not_taken_while_running()
    {
        var rig = await RunningRig();
        _ = rig.Probes.TakenPorts.Add(3000);

        var status = await Status(rig);

        var port = Assert.Single(status.Checks, static c => c.Name == "port");
        Assert.Equal(CheckState.Ok, port.State);
        Assert.Equal("3000 (Onyx's own)", port.Detail);
        Assert.Equal(0, status.ExitCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_is_stopped_when_docker_is_down_and_the_docker_row_fails()
    {
        var rig = await RunningRig();
        rig.Runtime.Facts = FakeContainerRuntime.DaemonDown;

        var status = await Status(rig);

        Assert.Equal(OnyxState.Stopped, status.State);
        var docker = Assert.Single(status.Checks, static c => c.Name == "docker");
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Equal(1, status.ExitCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_writes_nothing_and_starts_nothing()
    {
        var rig = await RunningRig();
        var (writes, stateWrites, calls) = (rig.Workspace.Writes.Count, rig.Workspace.StateWrites, rig.Runtime.Calls.Count);

        _ = await Status(rig);

        Assert.Equal(writes, rig.Workspace.Writes.Count);
        Assert.Equal(stateWrites, rig.Workspace.StateWrites);
        Assert.DoesNotContain(rig.Runtime.Calls.Skip(calls), static c => c.Equals("Pull", StringComparison.Ordinal) || c.Equals("Up", StringComparison.Ordinal) || c.Equals("Stop", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_real_pins_are_what_status_reports()
        => Assert.Equal(new EmbeddedOnyxPins().Pins.Version, new OnyxUpRig().Pins.Pins.Version);
}
