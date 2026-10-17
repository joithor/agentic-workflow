using System.Text.RegularExpressions;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed partial class GatePriorityTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_suite_unset_list_is_what_bash_tests_strips()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "ci", "bash-tests.sh"));
        var loop = ForLoop.Match(script);
        Assert.True(loop.Success, "the for-loop that builds the env -u flags in scripts/ci/bash-tests.sh");

        var stripped = loop.Groups["names"].Value.Replace("\\\n", " ", StringComparison.Ordinal).Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(stripped.Order(StringComparer.Ordinal), GatePriority.SuiteUnset.Order(StringComparer.Ordinal));
        Assert.All(GatePriority.Scrubbed, name => Assert.Contains(name, stripped, StringComparer.Ordinal));
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "global.json")) && File.Exists(Path.Combine(dir.FullName, "scripts", "ci", "bash-tests.sh")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("repo root not found");
    }

    [GeneratedRegex(@"for v in (?<names>[^;]+?); do\s*\n\s*unset_flags\+=", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ForLoop { get; }
}
