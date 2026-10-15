using Aw.Meta.Core.Commands;

namespace Aw.Meta.Tests.Commands;

public sealed class ProgramPathTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_relative_path_with_a_directory_resolves_against_the_working_directory()
    {
        Assert.Equal("/gate/tree/scripts/check.sh", ProgramPath.Resolve("scripts/check.sh", "/gate/tree"));
        Assert.Equal("/gate/tree/run.sh", ProgramPath.Resolve("./run.sh", "/gate/tree"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_name_an_absolute_path_or_no_working_directory_is_left_alone()
    {
        Assert.Equal("dotnet", ProgramPath.Resolve("dotnet", "/gate/tree"));
        Assert.Equal("/usr/bin/env", ProgramPath.Resolve("/usr/bin/env", "/gate/tree"));
        Assert.Equal("scripts/check.sh", ProgramPath.Resolve("scripts/check.sh", cwd: null));
        Assert.Equal("scripts/check.sh", ProgramPath.Resolve("scripts/check.sh", ""));
    }
}
