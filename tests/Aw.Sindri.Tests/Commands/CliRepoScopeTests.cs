using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Commands;

/// <summary>Which repo entry contains a main checkout (API spec §3.1, step 3): containment by whole path segments, longest wins.</summary>
public sealed class CliRepoScopeTests
{
    private static readonly RepoEntry[] _repos = [new("work", "/work"), new("demo", "/work/demo"), new("demo-two", "/work/demo-two/")];

    [Theory, Trait("Lane", "unit")]
    [InlineData("/work/demo", "demo")]
    [InlineData("/work/demo/packages/a", "demo")]
    [InlineData("/work/demo-two", "demo-two")]
    [InlineData("/work/demolition", "work")]
    [InlineData("/work", "work")]
    [InlineData("/elsewhere", null)]
    [InlineData("/wor", null)]
    public void The_longest_entry_containing_the_checkout_by_whole_segments_wins(string checkout, string? expected)
    {
        var repo = RepoScope.Longest(checkout, _repos);

        Assert.Equal(expected, repo?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_whose_name_isnt_a_repo_name_is_skipped()
    {
        var repo = RepoScope.Longest("/work/demo", [new RepoEntry("outer", "/work"), new RepoEntry("bad name", "/work/demo")]);

        Assert.Equal(RepoName.From("outer"), repo);
    }
}
