using Aw.Context.Infrastructure.Extract;
using Aw.Context.Infrastructure.Git;

namespace Aw.Context.Tests.Infrastructure;

public sealed class GraphifyClusterSourceTests
{
    private static readonly string[] _bwrapPaths = ["/usr/bin/bwrap", "/bin/bwrap"];

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
        using var stub = new StubGraphify("0.0.1");
        var source = new GraphifyClusterSource(new GitCli(), stub.Options);

        var set = await source.Clusters(repo.Repo, repo.Head, Ct);

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

        var set = await source.Clusters(repo.Repo, repo.Head, Ct);

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
    public async Task A_run_that_throws_for_an_unexpected_io_reason_is_a_degraded_unavailable_set_not_an_exception()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var source = new GraphifyClusterSource(new GitCli(), stub.Options with { Sandbox = (_, command, _) => command.Contains("extract", StringComparer.Ordinal) ? throw new IOException("disk full") : command });

        var set = await source.Clusters(repo.Repo, repo.Head, Ct);

        Assert.True(set.IsSuccess);
        Assert.False(set.Value.Available);
        Assert.False(File.Exists(stub.RanMarker));   // the --version probe passed and the run itself is what threw
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_programming_error_in_the_run_is_not_swallowed_as_degraded_clusters()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export const a = 1;\n" });
        using var stub = new StubGraphify(GraphifyOptions.Default.Pin);
        var source = new GraphifyClusterSource(new GitCli(), stub.Options with { Sandbox = (_, command, _) => command.Contains("extract", StringComparer.Ordinal) ? throw new ArgumentNullException(nameof(command)) : command });

        _ = await Assert.ThrowsAsync<ArgumentNullException>(async () => await source.Clusters(repo.Repo, repo.Head, Ct));
    }

    /// <summary>
    /// The real pinned graphify under the real sandbox on a small TS repo whose files import each other, one of them under a
    /// deny glob. Where the machine can't run it (no root-owned bwrap that starts, no graphify at the pin on PATH) the test is
    /// skipped with the reason; where it can, it must cluster: a silent pass without graphify running is not allowed.
    /// </summary>
    [Fact, Trait("Lane", "heavy")]
    public async Task The_real_graphify_clusters_a_small_repo_under_the_sandbox_and_never_sees_a_denied_file()
    {
        var unrunnable = WhyGraphifyCannotRun();
        Assert.SkipWhen(unrunnable is not null, unrunnable ?? string.Empty);
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/app.ts"] = "import { open } from './layout/pane';\nimport { bind } from './keys/bindings';\nexport const app = () => [open(), bind()];\n",
            ["src/layout/pane.ts"] = "import { size } from './size';\nexport function open() { return size() + 1; }\n",
            ["src/layout/size.ts"] = "export function size() { return 2; }\n",
            ["src/keys/bindings.ts"] = "import { open } from '../layout/pane';\nimport { token } from '../../config/secrets/keys';\nexport function bind() { return [open(), token]; }\n",
            ["config/secrets/keys.ts"] = "export const token = 'not-a-real-secret';\n",
        });
        var source = new GraphifyClusterSource(new GitCli(), GraphifyOptions.Default);

        var set = await source.Clusters(repo.Repo, repo.Head, Ct);

        Assert.True(set.IsSuccess);
        Assert.True(set.Value.Available, "graphify and the sandbox are usable here, so the set must be available");
        Assert.NotEmpty(set.Value.Clusters);
        var members = set.Value.Clusters.SelectMany(c => c.Files).ToList();
        Assert.NotEmpty(members);
        var tracked = repo.Run("ls-files").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(members, f => Assert.Contains(f, tracked, StringComparer.Ordinal));
        Assert.DoesNotContain(members, f => f.Contains("secrets/", StringComparison.Ordinal));
        Assert.Contains(members, f => f.EndsWith("pane.ts", StringComparison.Ordinal));
        TestContext.Current.SendDiagnosticMessage($"graphify produced {set.Value.Clusters.Count} cluster(s) over {members.Count} file(s)");
    }

    // Null when the real run is possible; else the reason to skip. A root-owned bwrap that starts and a graphify on PATH printing the pin.
    private static string? WhyGraphifyCannotRun()
    {
        if (!OperatingSystem.IsLinux())
        {
            return "the real-graphify run is only wired for Linux (bwrap)";
        }

        var bwrap = _bwrapPaths.FirstOrDefault(File.Exists);
        if (bwrap is null || Probe(bwrap, "--ro-bind", "/", "/", "true") is not { } sandboxed || sandboxed.Exit != 0)
        {
            return "no root-owned bwrap that can start a sandbox here";
        }

        var graphify = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, "graphify")).FirstOrDefault(File.Exists);
        return graphify is null ? "no graphify on PATH"
            : Probe(graphify, "--version") is { Exit: 0 } version && version.Output.Contains(GraphifyOptions.Default.Pin, StringComparison.Ordinal) ? null
            : $"the graphify on PATH is not the pin {GraphifyOptions.Default.Pin}";
    }

    private static (int Exit, string Output)? Probe(string file, params string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        try
        {
            using var process = System.Diagnostics.Process.Start(start);
            if (process is null)
            {
                return null;
            }

            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var output = process.StandardOutput.ReadToEnd();
            return process.WaitForExit(TimeSpan.FromSeconds(60)) ? (process.ExitCode, output + stderr.Result) : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
