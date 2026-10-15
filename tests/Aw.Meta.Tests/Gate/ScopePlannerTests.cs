using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class ScopePlannerTests
{
    private static readonly GateConfig _gate = new(
        ["repo"],
        ["**/*.md", "docs/**"],
        new Dictionary<string, ScopeConfig>(StringComparer.Ordinal)
        {
            ["repo"] = new(["**"], []),
            ["dotnet"] = new(["src/**", "tests/**", "*.props", "global.json"], []),
            ["bash-lib"] = new(["config/lib/**"], []),
        });

    [Theory, Trait("Lane", "unit")]
    [InlineData("README.md", "repo")]
    [InlineData("README.md,docs/a/b.png", "repo")]
    [InlineData("src/A/B.cs", "repo,dotnet")]
    [InlineData("src/A/B.cs,docs/x.md", "repo,dotnet")]
    [InlineData("Directory.Build.props", "repo,dotnet")]
    [InlineData("config/lib/x.sh,src/y.cs", "repo,dotnet,bash-lib")]
    [InlineData("config/lib/x.sh", "repo,bash-lib")]
    [InlineData("unrelated/file.txt", "repo")]
    [InlineData("", "repo")]
    public void Scopes_follow_the_changed_paths(string changed, string expected)
    {
        var paths = changed.Length == 0 ? [] : changed.Split(',');

        Assert.Equal(expected.Split(','), ScopePlanner.Plan(paths, _gate));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_gate_plans_nothing() =>
        Assert.Empty(ScopePlanner.Plan(["src/a.cs"], GateConfig.Empty));
}
