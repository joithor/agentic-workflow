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
    public void A_tool_whose_absolute_path_does_not_exist_keeps_it_so_running_it_fails_as_AW_META_010_never_through_PATH()
    {
        var paths = MacToolPaths.Resolve(_ => false);

        Assert.Equal(["/usr/sbin/sysctl", "/usr/bin/vm_stat", "/bin/ps", "/bin/kill", "/usr/sbin/lsof"], [paths.Sysctl, paths.VmStat, paths.Ps, paths.Kill, paths.Lsof]);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_tool_is_ever_a_bare_name()
    {
        var paths = MacToolPaths.Resolve(path => path == "/bin/ps");

        Assert.Equal(["/usr/sbin/sysctl", "/bin/ps"], [paths.Sysctl, paths.Ps]);
    }
}
