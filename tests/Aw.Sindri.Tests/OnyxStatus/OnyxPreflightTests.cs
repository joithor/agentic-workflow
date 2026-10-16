using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class OnyxPreflightTests
{
    private const long Gib = 1L << 30;

    private static readonly OnyxPins _pins = new(
        "v4.9.0", new string('0', 40), [], [], DiskBytes: 30 * Gib, DockerMemoryBytes: 5_905_580_032);   // the lock's floor: 5.5 GiB of MemTotal for a 6 GiB VM

    private static readonly DiskFacts _roomy = new("/h", 80 * Gib);
    private static readonly PortFacts _free = new(3000, PreferredFree: true, FirstFreeFallback: 3001);

    private static PreflightCheck Check(PreflightReport report, string name) => Assert.Single(report.Checks, c => c.Name == name);

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_all_ok_returns_port_3000()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, _roomy, _free, _pins);

        Assert.True(report.Ok);
        Assert.Equal(3000, report.Port);
        Assert.Equal(["docker", "compose", "memory", "disk", "port"], report.Checks.Select(static c => c.Name), StringComparer.Ordinal);
        Assert.Equal("29.4.0", Check(report, "docker").Detail);
        Assert.Equal("v2.40.3", Check(report, "compose").Detail);
        Assert.Equal("15.7 GiB for Docker, needs a 6 GiB setting", Check(report, "memory").Detail);
        Assert.Equal("80.0 GiB free at /h, needs 30.0 GiB", Check(report, "disk").Detail);
        Assert.Equal("3000 free", Check(report, "port").Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_001_with_an_install_fix_when_the_cli_is_missing()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.NoCli, _roomy, _free, _pins);

        var docker = Check(report, "docker");
        Assert.Equal(CheckState.Fail, docker.State);
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Contains("install Docker", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.False(report.Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_001_with_a_start_fix_when_the_daemon_is_not_answering()
    {
        // Docker installed, Docker Desktop not started: the fix says start it, not install it.
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.DaemonDown, _roomy, _free, _pins);

        var docker = Check(report, "docker");
        Assert.Equal("SND-ONYX-001", docker.Problem?.Code);
        Assert.Contains("start Docker Desktop", docker.Problem?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("install", docker.Problem?.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CheckState.Warn, Check(report, "compose").State);
        Assert.Equal("needs docker", Check(report, "compose").Detail);
        Assert.Equal(CheckState.Warn, Check(report, "memory").State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_002_when_only_compose_v1_is_present()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.ComposeV1Only, _roomy, _free, _pins);

        var compose = Check(report, "compose");
        Assert.Equal(CheckState.Fail, compose.State);
        Assert.Equal("SND-ONYX-002", compose.Problem?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_003_when_docker_memory_is_short()
    {
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = 4 * Gib };

        var memory = Check(OnyxPreflight.Evaluate(docker, _roomy, _free, _pins), "memory");

        Assert.Equal(CheckState.Fail, memory.State);
        Assert.Equal("4.0 GiB for Docker, needs a 6 GiB setting", memory.Detail);
        Assert.Equal("SND-ONYX-003", memory.Problem?.Code);
        Assert.Contains("set Docker's memory to at least 6 GiB in Docker Desktop → Settings → Resources", memory.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_passes_a_6_GiB_vm_whose_MemTotal_reads_5_8_GiB()
    {
        // Docker Desktop's `docker info` MemTotal sits a few percent below the VM limit the user set.
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = (long)(5.8 * Gib) };

        var memory = Check(OnyxPreflight.Evaluate(docker, _roomy, _free, _pins), "memory");

        Assert.Equal(CheckState.Ok, memory.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_a_MemTotal_of_5_4_GiB_with_the_6_GiB_fix()
    {
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = (long)(5.4 * Gib) };

        var memory = Check(OnyxPreflight.Evaluate(docker, _roomy, _free, _pins), "memory");

        Assert.Equal(CheckState.Fail, memory.State);
        Assert.Equal("5.4 GiB for Docker, needs a 6 GiB setting", memory.Detail);
        Assert.Contains("at least 6 GiB in Docker Desktop", memory.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_warns_when_docker_memory_is_unknown()
    {
        var docker = FakeContainerRuntime.Healthy with { MemoryBytes = null };

        var memory = Check(OnyxPreflight.Evaluate(docker, _roomy, _free, _pins), "memory");

        Assert.Equal(CheckState.Warn, memory.State);
        Assert.Null(memory.Problem);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_003_when_disk_is_short()
    {
        var disk = Check(OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, new DiskFacts("/h", 11 * Gib), _free, _pins), "disk");

        Assert.Equal(CheckState.Fail, disk.State);
        Assert.Equal("11.0 GiB free at /h, needs 30.0 GiB", disk.Detail);
        Assert.Equal("SND-ONYX-003", disk.Problem?.Code);
        Assert.Contains("11.0 GiB free at /h, needs 30.0 GiB", disk.Problem?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_uses_the_first_free_fallback()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, _roomy, new PortFacts(3000, PreferredFree: false, FirstFreeFallback: 3002), _pins);

        var port = Check(report, "port");
        Assert.Equal(CheckState.Ok, port.State);
        Assert.Equal("3000 taken; would use 3002", port.Detail);
        Assert.Equal(3002, report.Port);
    }

    [Fact, Trait("Lane", "unit")]
    public void Evaluate_fails_006_when_no_port_is_free()
    {
        var report = OnyxPreflight.Evaluate(FakeContainerRuntime.Healthy, _roomy, new PortFacts(3000, PreferredFree: false, FirstFreeFallback: null), _pins);

        var port = Check(report, "port");
        Assert.Equal(CheckState.Fail, port.State);
        Assert.Equal("SND-ONYX-006", port.Problem?.Code);
        Assert.Equal(3000, report.Port);
        Assert.False(report.Ok);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_catalogue_lists_every_onyx_code_the_preflight_can_return()
    {
        var codes = SindriErrors.All.Select(static e => e.Code).Where(static c => c.StartsWith("SND-ONYX-", StringComparison.Ordinal)).Order(StringComparer.Ordinal);

        Assert.Equal(["SND-ONYX-001", "SND-ONYX-002", "SND-ONYX-003", "SND-ONYX-004", "SND-ONYX-006"], codes, StringComparer.Ordinal);
    }
}
