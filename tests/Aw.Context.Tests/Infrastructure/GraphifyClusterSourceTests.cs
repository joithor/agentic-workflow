using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;

namespace Aw.Context.Tests.Infrastructure;

public sealed class GraphifyClusterSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<Kernel.Result<Context.Core.Extract.ClusterSet>> Clusters(GraphifyClusterSource source, TempGitRepo repo, TimeSpan? deadline = null) =>
        GraphifyRun.Clusters(source, repo, ClusterLimits.Default, deadline);

    [Fact, Trait("Lane", "integration")]
    public async Task An_absent_graphify_is_an_empty_unavailable_set_not_an_error()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        var source = new GraphifyClusterSource(new GitCli(), GraphifyOptions.Default with { Binary = "/nonexistent/graphify" });

        var set = await Clusters(source, repo);

        Assert.True(set.IsSuccess);
        Assert.False(set.Value.Available);
        Assert.Empty(set.Value.Clusters);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_graphify_of_another_version_is_not_run_on_a_repo()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify("0.0.1");
        var source = new GraphifyClusterSource(new GitCli(), stub.Options);

        var set = await Clusters(source, repo);

        Assert.True(set.IsSuccess);
        Assert.False(set.Value.Available);
        Assert.False(File.Exists(stub.RanMarker), "a graphify of another version must not be run on the repo");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pinned_graphify_runs_in_a_private_folder_inside_the_snapshot_and_its_communities_become_clusters()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n", ["src/b.ts"] = "export const b = 2;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var source = new GraphifyClusterSource(new GitCli(), stub.Options);

        var set = await Clusters(source, repo);

        Assert.True(set.IsSuccess);
        Assert.True(set.Value.Available);
        var cluster = Assert.Single(set.Value.Clusters);
        Assert.Equal(["src/a.ts", "src/b.ts"], cluster.Files);
        var env = (await File.ReadAllTextAsync(stub.RanMarker, Ct)).Trim().Split('|');
        Assert.Equal(env[0], env[1]);   // HOME and TMPDIR are the one private folder
        Assert.Contains("/aw-graphify-", env[0], StringComparison.Ordinal);   // inside the snapshot, not the system temp folder itself
        Assert.Contains(GraphifyClusterSource.PrivateFolder, env[0], StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal("700", env[2]);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_stub_script_whose_write_descriptor_a_concurrent_fork_still_holds_is_still_run()
    {
        // Linux refuses to exec a file some process holds open for writing (ETXTBSY). A sibling test's fork can inherit the
        // write descriptor of a stub script that was just written, so the stub must never be exec'd itself, only read by sh.
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        await using var heldOpen = new FileStream(stub.Options.Binary, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        var source = new GraphifyClusterSource(new GitCli(), stub.Options);

        var set = await Clusters(source, repo);

        Assert.True(set.Value.Available, "the stub ran despite an open write descriptor");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_run_that_throws_for_an_unexpected_io_reason_is_a_degraded_unavailable_set_not_an_exception()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var source = new GraphifyClusterSource(new GitCli(), stub.Options with { Sandbox = (_, command, _) => command.Contains("extract", StringComparer.Ordinal) ? throw new IOException("disk full") : StubGraphify.RunThroughShell(command) });

        var set = await Clusters(source, repo);

        Assert.True(set.IsSuccess);
        Assert.False(set.Value.Available);
        Assert.False(File.Exists(stub.RanMarker));   // the --version probe passed and the run itself is what threw
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_programming_error_in_the_run_is_not_swallowed_as_degraded_clusters()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var source = new GraphifyClusterSource(new GitCli(), stub.Options with { Sandbox = (_, command, _) => command.Contains("extract", StringComparer.Ordinal) ? throw new ArgumentNullException(nameof(command)) : StubGraphify.RunThroughShell(command) });

        var thrown = await Record.ExceptionAsync(async () => await Clusters(source, repo));

        if (thrown is TimeoutException)
        {
            Assert.Fail(thrown.Message);   // a hang, not the programming error this test is about
        }

        _ = Assert.IsType<ArgumentNullException>(thrown);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_graphify_run_that_never_ends_fails_the_test_fast_with_a_message_naming_the_hang()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin, hangs: true);
        var source = new GraphifyClusterSource(new GitCli(), stub.Options);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var thrown = await Assert.ThrowsAsync<TimeoutException>(async () => await Clusters(source, repo, TimeSpan.FromSeconds(3)));

        Assert.Contains("did not finish in 3 s", thrown.Message, StringComparison.Ordinal);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(20), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the deadline must cut the hang short, it took {started.Elapsed.TotalSeconds} s"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Unpack_writes_source_files_and_leaves_out_a_file_under_a_deny_glob()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/keys/bindings.ts"] = "export const bind = 1;\n",
            ["config/secrets/keys.ts"] = "export const token = 'not-a-real-secret';\n",
        });
        var snapshot = Directory.CreateTempSubdirectory("aw-unpack-").FullName;
        try
        {
            var exported = await GraphifyRun.Archive(new GitCli(), repo, (stream, token) => GraphifySnapshot.Unpack(stream, snapshot, repo.Repo, new GraphifySnapshot.Tally(ClusterLimits.Default), token));

            Assert.False(exported.IsError);
            Assert.True(File.Exists(Path.Combine(snapshot, "src", "keys", "bindings.ts")), "a source file is unpacked");
            Assert.False(File.Exists(Path.Combine(snapshot, "config", "secrets", "keys.ts")), "a denied file never reaches graphify");
        }
        finally
        {
            Directory.Delete(snapshot, recursive: true);
        }
    }

    /// <summary>
    /// The real pinned graphify under the real sandbox on a small TS repo whose files import each other, one of them under a
    /// deny glob. Where the machine can't run it (the production probe says why: no sandbox, no graphify, or not the pin) the test is
    /// skipped with the reason; where it can, it must cluster: a silent pass without graphify running is not allowed.
    /// </summary>
    [Fact, Trait("Lane", "heavy")]
    public async Task The_real_graphify_clusters_a_small_repo_under_the_sandbox_and_never_sees_a_denied_file()
    {
        var source = new GraphifyClusterSource(new GitCli(), GraphifyOptions.Default);
        var unrunnable = await source.WhyUnavailable(Ct);
        Assert.SkipWhen(unrunnable is not null, $"the real graphify can't run here: {unrunnable}");
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/app.ts"] = "import { open } from './layout/pane';\nimport { bind } from './keys/bindings';\nexport const app = () => [open(), bind()];\n",
            ["src/layout/pane.ts"] = "import { size } from './size';\nexport function open() { return size() + 1; }\n",
            ["src/layout/size.ts"] = "export function size() { return 2; }\n",
            ["src/keys/bindings.ts"] = "import { open } from '../layout/pane';\nimport { token } from '../../config/secrets/keys';\nexport function bind() { return [open(), token]; }\n",
            ["config/secrets/keys.ts"] = "export const token = 'not-a-real-secret';\n",
        });

        var set = await Clusters(source, repo);

        Assert.True(set.IsSuccess);
        Assert.True(set.Value.Available, "graphify and the sandbox are usable here, so the set must be available");
        Assert.NotEmpty(set.Value.Clusters);
        var members = set.Value.Clusters.SelectMany(c => c.Files).ToList();
        Assert.NotEmpty(members);
        var tracked = repo.Run("ls-files").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(members, f => Assert.Contains(f, tracked, StringComparer.Ordinal));
        Assert.DoesNotContain(members, f => f.Contains("secrets/", StringComparison.Ordinal));
        Assert.Contains(members, f => f.EndsWith("pane.ts", StringComparison.Ordinal));
        Assert.Contains("src/keys/bindings.ts", members, StringComparer.Ordinal);   // the file that imports the denied one is clustered, so the absence of keys.ts below means something
        TestContext.Current.SendDiagnosticMessage($"graphify produced {set.Value.Clusters.Count} cluster(s) over {members.Count} file(s)");
    }
}
