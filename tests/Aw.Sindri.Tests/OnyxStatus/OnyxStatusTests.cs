using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Infrastructure.Onyx;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxStatus;

public sealed class OnyxStatusTests
{
    private const long Gib = 1L << 30;

    private static async Task<OnyxStatusResponse> Status(long freeBytes)
    {
        var probes = new FakeHostProbes { FreeBytes = freeBytes };
        _ = probes.Directories.Add("/sindri-home");
        var service = new OnyxStatusService(new EmbeddedOnyxPins(), new FakeContainerRuntime(FakeContainerRuntime.Healthy), probes, new FakeSindriHome("/sindri-home"));

        return (await service.Handle(new OnyxStatusRequest(), TestContext.Current.CancellationToken)).Value;
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
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_exit_1_when_disk_is_short()
    {
        var status = await Status(11 * Gib);

        Assert.Equal(1, status.ExitCode);
        Assert.Equal("SND-ONYX-003", status.Checks.Single(static c => c.Name == "disk").Problem?.Code);
    }
}
