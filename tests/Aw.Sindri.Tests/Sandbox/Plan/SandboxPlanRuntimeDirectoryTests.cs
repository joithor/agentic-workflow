using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The two additions the bubblewrap adapter needs from the plan (spike item 1, notes 1 and 3): a /run tmpfs and the state directory the hooks look in.</summary>
public sealed class SandboxPlanRuntimeDirectoryTests
{
    [Fact, Trait("Lane", "unit")]
    public void Run_is_a_small_tmpfs_before_the_net_directory_is_bound()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var run = plan.Mounts.ToList().FindIndex(m => m.Path == "/run");
        var net = plan.Mounts.ToList().FindIndex(m => m.Path == "/run/sindri/net");
        Assert.True(run >= 0 && run < net);
        Assert.Equal(PlanMountKind.Tmpfs, plan.Mounts[run].Kind);
        Assert.True(plan.Mounts[run].Size is > 0 and <= 64L * 1024 * 1024);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_state_directory_is_not_in_the_environment() =>
        Assert.DoesNotContain("AW_STATE_DIR", SandboxPlannerTests.Build(Req.WriteScope).Environment.Select(e => e.Name), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_state_directory_elsewhere_is_in_the_environment_so_the_hooks_find_it()
    {
        var host = Req.Host with { StateDir = "/srv/aw", SindriHome = "/srv/aw/sindri-app" };

        var plan = SandboxPlannerTests.Build(Req.WriteScope with
        {
            Host = host,
            Paths = Req.WriteScope.Paths with { RunDirectory = "/srv/aw/sindri-app/runs/x/s/1", RunArea = "/srv/aw/sindri-app/areas/run-1" },
        });

        Assert.Equal("/srv/aw", plan.Environment.Single(e => e.Name == "AW_STATE_DIR").Value);
    }
}
