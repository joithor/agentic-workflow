using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Testing.Fixtures;

namespace Aw.Context.Tests.Infrastructure;

public sealed class GraphifyClusterSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task An_absent_graphify_is_an_empty_unavailable_set_not_an_error()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        var source = new GraphifyClusterSource(new GitCli(), GraphifyOptions.Default with { Binary = "/nonexistent/graphify" });

        var set = await source.Clusters(repo.Repo, repo.Head, Ct);

        Assert.True(set.IsSuccess);
        Assert.False(set.Value.Available);
        Assert.Empty(set.Value.Clusters);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_graphify_of_another_version_is_not_run_on_a_repo()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        var source = new GraphifyClusterSource(new GitCli(), GraphifyOptions.Default with { Pin = "0.0.1" });

        var set = await source.Clusters(repo.Repo, repo.Head, Ct);

        Assert.False(set.Value.Available);
    }

    /// <summary>
    /// The real pinned graphify under the real sandbox on the fixture app at its head commit. Where the machine's sandbox
    /// can't start (an AppArmor-restricted user namespace for bwrap), graphify must not run at all: the set is unavailable.
    /// </summary>
    [Fact, Trait("Lane", "heavy")]
    public async Task The_fixture_app_clusters_under_the_sandbox_or_degrades_without_running_graphify()
    {
        using var fixture = FixtureRepo.Build();
        var repo = new Context.Core.Engine.ContextRepo(Context.Core.Engine.ContextRepoName.From("aw-split-panes"), fixture.Path);
        var source = new GraphifyClusterSource(new GitCli(), GraphifyOptions.Default);

        var set = await source.Clusters(repo, fixture.Head, Ct);

        Assert.True(set.IsSuccess);
        if (!set.Value.Available)
        {
            Assert.Empty(set.Value.Clusters);   // no usable sandbox here: graphify never ran unsandboxed
            return;
        }

        Assert.NotEmpty(set.Value.Clusters);
        Assert.All(set.Value.Clusters.SelectMany(c => c.Files), f => Assert.False(Path.IsPathRooted(f)));
        Assert.Contains(set.Value.Clusters.SelectMany(c => c.Files), f => f.EndsWith("SplitPane.ts", StringComparison.Ordinal));
    }
}
