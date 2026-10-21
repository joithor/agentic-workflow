using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>The reason a cluster pass gives when it is degraded (the build reports it as 'clusters: reason').</summary>
[Trait("Lane", "integration")]
public sealed class GraphifyClusterReasonTests
{
    private static TempGitRepo Repo(Dictionary<string, string>? extra = null)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n", ["src/b.ts"] = "export const b = 2;\n" };
        foreach (var (path, text) in extra ?? [])
        {
            files[path] = text;
        }

        return new TempGitRepo(files);
    }

    private static async Task<ClusterSet> Cluster(TempGitRepo repo, GraphifyOptions options, ClusterLimits limits)
    {
        var set = await GraphifyRun.Clusters(new GraphifyClusterSource(new GitCli(), options), repo, limits);
        Assert.True(set.IsSuccess);
        return set.Value;
    }

    private static GraphifyOptions Through(StubGraphify stub, string script) =>
        stub.Options with { Sandbox = (_, command, _) => command.Contains("extract", StringComparer.Ordinal) ? ["/bin/sh", "-c", script, "sh", command[2]] : StubGraphify.RunThroughShell(command) };

    [Fact]
    public async Task A_missing_graphify_is_not_found()
    {
        using var repo = Repo();

        var set = await Cluster(repo, GraphifyOptions.Default with { Binary = "/nonexistent/graphify" }, ClusterLimits.Default);

        Assert.Equal((false, "graphify not found"), (set.Available, set.Reason));
    }

    [Fact]
    public async Task A_machine_without_a_sandbox_says_so()
    {
        using var repo = Repo();
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);

        var set = await Cluster(repo, stub.Options with { Sandbox = (_, _, _) => null }, ClusterLimits.Default);

        Assert.Equal((false, "no sandbox"), (set.Available, set.Reason));
    }

    [Fact]
    public async Task Another_version_names_the_pin()
    {
        using var repo = Repo();
        using var stub = new StubGraphify("0.0.1");

        var set = await Cluster(repo, stub.Options, ClusterLimits.Default);

        Assert.Equal((false, $"graphify is not the pin {GraphifyOptions.Default.Pin}"), (set.Available, set.Reason));
    }

    [Fact]
    public async Task A_run_over_the_time_limit_is_killed_and_says_how_long_it_was_given()
    {
        using var repo = Repo();
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var options = Through(stub, "sleep 30") with { MinuteLength = TimeSpan.FromMilliseconds(400) };

        var set = await Cluster(repo, options, new ClusterLimits { TimeoutMinutes = 1 });

        Assert.Equal((false, "timed out after 1 min"), (set.Available, set.Reason));
    }

    [Fact]
    public async Task A_graph_over_the_cap_is_not_read_and_says_so()
    {
        using var repo = Repo();
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var script = "mkdir -p \"$1/graphify-out\" && head -c 1500000 /dev/zero > \"$1/graphify-out/graph.json\"";

        var set = await Cluster(repo, Through(stub, script), new ClusterLimits { MaxGraphMB = 1 });
        var unlimited = await Cluster(repo, Through(stub, script), new ClusterLimits { MaxGraphMB = 0 });

        Assert.Equal((false, "graph.json over 1 MB"), (set.Available, set.Reason));
        Assert.Equal("graph.json could not be read", unlimited.Reason);   // no cap: the file was read, and is not a graph
    }

    [Fact]
    public async Task A_truncated_snapshot_that_still_clusters_says_it_was_truncated()
    {
        using var repo = Repo(new Dictionary<string, string>(StringComparer.Ordinal) { ["big/x1.ts"] = new string('a', 700 * 1024), ["big/x2.ts"] = new string('b', 700 * 1024) });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);

        var set = await Cluster(repo, stub.Options, new ClusterLimits { MaxSnapshotMB = 1 });

        Assert.True(set.Available);
        Assert.NotEmpty(set.Clusters);
        Assert.Equal("snapshot truncated at 1 MB (1 file skipped)", set.Reason);
    }

    [Fact]
    public async Task A_file_over_the_per_file_cap_is_reported_as_left_out()
    {
        using var repo = Repo(new Dictionary<string, string>(StringComparer.Ordinal) { ["data/big.json"] = new string('a', 1536 * 1024) });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);

        var capped = await Cluster(repo, stub.Options, new ClusterLimits { MaxFileMB = 1 });
        var unlimited = await Cluster(repo, stub.Options, new ClusterLimits { MaxFileMB = 0 });

        Assert.Equal((true, "1 file over 1 MB left out"), (capped.Available, capped.Reason));
        Assert.Equal((true, null), (unlimited.Available, unlimited.Reason));
    }

    [Fact]
    public async Task A_whole_snapshot_that_clusters_has_no_reason()
    {
        using var repo = Repo();
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);

        var set = await Cluster(repo, stub.Options, ClusterLimits.Default);

        Assert.Equal((true, null), (set.Available, set.Reason));
    }
}
