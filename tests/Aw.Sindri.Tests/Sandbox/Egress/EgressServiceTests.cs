using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressServiceTests
{
    private static EgressService Service(Func<string, string?> env) =>
        new(new FakeEgressResolver([]), new FakeEgressDialer(), env, Directory.CreateTempSubdirectory("egress-run").FullName);

    private static PlanNetwork Network(string dir) => new(NetworkMode.Enforce, [], 7480, 3128, dir);

    [Fact, Trait("Lane", "unit")]
    public void Missing_socat_is_014_no_forwarder()
    {
        var result = Service(n => n == "AW_SANDBOX_SOCAT" ? "/nonexistent/socat" : null).Start(Network("/tmp/sindri-x/a1b2c3d4"), EgressCaps.Default);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSandboxUnavailable.Code, result.Error.Code);
        Assert.Equal(SandboxReasons.NoForwarder, SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_socket_directory_that_does_not_fit_is_016_net_forward()
    {
        var socat = Path.Combine(Directory.CreateTempSubdirectory("egress-socat").FullName, "socat");
        File.WriteAllText(socat, "#!/bin/sh\n");

        var result = Service(n => n == "AW_SANDBOX_SOCAT" ? socat : null).Start(Network("/tmp/" + new string('d', 100)), EgressCaps.Default);

        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal(SandboxReasons.NetForward, SandboxRefusal.ReasonOf(result.Error));
    }
}
