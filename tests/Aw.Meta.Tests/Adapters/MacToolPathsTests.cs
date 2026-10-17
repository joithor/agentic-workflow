using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class MacToolPathsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Each_tool_is_the_absolute_path_macOS_ships_it_at_when_that_file_exists()
    {
        var paths = MacToolPaths.Resolve(_ => true);

        Assert.Equal(["/usr/sbin/sysctl", "/usr/bin/vm_stat", "/bin/ps", "/bin/kill", "/usr/sbin/lsof"], [paths.Sysctl, paths.VmStat, paths.Ps, paths.Kill, paths.Lsof]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tool_whose_absolute_path_does_not_exist_falls_back_to_its_bare_name()
    {
        var paths = MacToolPaths.Resolve(_ => false);

        Assert.Equal(["sysctl", "vm_stat", "ps", "kill", "lsof"], [paths.Sysctl, paths.VmStat, paths.Ps, paths.Kill, paths.Lsof]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_tool_is_resolved_on_its_own()
    {
        var paths = MacToolPaths.Resolve(path => path == "/bin/ps");

        Assert.Equal(["sysctl", "/bin/ps"], [paths.Sysctl, paths.Ps]);
    }
}
