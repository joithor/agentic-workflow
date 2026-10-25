using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Meta.Infrastructure.Processes;

namespace Aw.Meta.Tests.Cli;

/// <summary>The real aw-meta process: a SIGTERM during a train ends it, writes the train's end line and leaves no step running.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class QueueGateSignalTests : IDisposable
{
    private static readonly string[] _gitIdentity = ["-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false", "-c", "init.defaultBranch=main"];
    private readonly CliHarness _cli = new();

    [Fact, Trait("Lane", "integration")]
    public async Task A_SIGTERM_ends_queue_gate_with_the_train_ended_and_no_step_left_running()
    {
        var pidFile = _cli.File("step.pid");
        await Configure(pidFile);
        using var aw = Start();
        try
        {
            var step = ProcessTree.Snapshot(await WaitForPid(pidFile, aw))[0];
            await Signal(aw.Id);
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(60));
            await aw.WaitForExitAsync(bound.Token);

            var status = await _cli.Run("status");
            Assert.Contains("TRAIN pid " + aw.Id.ToString(CultureInfo.InvariantCulture), status.Out, StringComparison.Ordinal);
            Assert.Contains("TRAIN ended", status.Out, StringComparison.Ordinal);
            Assert.DoesNotContain("TRAIN stale", status.Out, StringComparison.Ordinal);
            for (var i = 0; i < 100 && ProcessTree.IsAlive(step); i++)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }

            Assert.False(ProcessTree.IsAlive(step), "the step is still running");
        }
        finally
        {
            if (!aw.HasExited)
            {
                aw.Kill(entireProcessTree: true);   // the test's own aw-meta, by its pid
            }
        }
    }

    private Process Start()
    {
        var host = typeof(Host.Composition).Assembly.Location;
        var info = new ProcessStartInfo("dotnet", [host, "queue-gate", "7"]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var name in info.Environment.Keys.Where(k => k.StartsWith("AW_META_", StringComparison.Ordinal) || k.StartsWith("TMUX", StringComparison.Ordinal)).ToList())
        {
            _ = info.Environment.Remove(name);   // the ambient team of whoever runs the tests
        }

        foreach (var name in new[] { "AW_STATE_DIR", "AW_CLAUDE_SESSIONS_DIR", "AW_TMUX", "AW_META_CONFIG", "AW_GH", "AW_HEAVY" })
        {
            info.Environment[name] = _cli.Env(name);
        }

        return Process.Start(info) ?? throw new InvalidOperationException("dotnet did not start");
    }

    private async Task Configure(string pidFile)
    {
        var (repo, head) = await Repository();
        var config = """{"team":"t","repo":"REPO","lanes":["api"],"metaLane":"meta","gate":{"always":["repo"],"docsOnly":["**/*.md"],"scopes":{"repo":{"paths":["**"],"steps":[{"name":"long","run":["sh","-c","sleep 120 & echo $! > PIDFILE; wait"],"termGraceSeconds":2}]}}}}""";
        await File.WriteAllTextAsync(_cli.File("team.json"), config.Replace("REPO", repo, StringComparison.Ordinal).Replace("PIDFILE", pidFile, StringComparison.Ordinal), TestContext.Current.CancellationToken);
        var gh = _cli.File("gh");
        _ = ExecutableScript.Write(
            gh,
            string.Join('\n',
            [
                "case \"$1 $2\" in",
                $"  \"pr view\") echo '{{\"headRefOid\":\"{head}\",\"headRefName\":\"feat\",\"isDraft\":false,\"title\":\"Add\",\"state\":\"OPEN\"}}' ;;",
                "  *) exit 9 ;;",
                "esac",
            ]) + "\n");
        _cli.SetEnv("AW_META_CONFIG", _cli.File("team.json"));
        _cli.SetEnv("AW_GH", gh);
        _cli.SetEnv("AW_HEAVY", _cli.File("no-such-heavy"));
    }

    private static async Task<int> WaitForPid(string file, Process aw)
    {
        for (var i = 0; i < 600; i++)
        {
            if (aw.HasExited)
            {
                throw new InvalidOperationException("aw-meta exited early: " + await aw.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken));
            }

            if (File.Exists(file) && int.TryParse((await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).Trim(), out var pid))
            {
                return pid;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("the step never started");
    }

    private static async Task Signal(int pid)
    {
        using var kill = Process.Start(new ProcessStartInfo("kill", ["-TERM", pid.ToString(CultureInfo.InvariantCulture)]) { UseShellExecute = false }) ?? throw new InvalidOperationException("kill did not start");
        await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
    }

    private async Task<(string Repo, string Head)> Repository()
    {
        var origin = _cli.File("origin.git");
        var repo = _cli.File("work");
        _ = Directory.CreateDirectory(origin);
        _ = Directory.CreateDirectory(repo);
        _ = Git(origin, "init", "--bare", "--quiet");
        _ = Git(repo, "init", "--quiet");
        _ = Git(repo, "remote", "add", "origin", origin);
        await File.WriteAllTextAsync(Path.Combine(repo, "main.txt"), "main", TestContext.Current.CancellationToken);
        _ = Git(repo, "add", ".");
        _ = Git(repo, "commit", "--quiet", "-m", "main");
        _ = Git(repo, "push", "--quiet", "origin", "HEAD:refs/heads/main");
        _ = Git(repo, "checkout", "--quiet", "-b", "feat");
        await File.WriteAllTextAsync(Path.Combine(repo, "feature.txt"), "feature", TestContext.Current.CancellationToken);
        _ = Git(repo, "add", ".");
        _ = Git(repo, "commit", "--quiet", "-m", "feature");
        _ = Git(repo, "push", "--quiet", "origin", "HEAD:refs/pull/7/head");
        var head = Git(repo, "rev-parse", "HEAD");
        _ = Git(repo, "checkout", "--quiet", "main");
        return (repo, head);
    }

    private static string Git(string directory, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in _gitIdentity.Concat(args))
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("git did not start");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : throw new InvalidOperationException($"git {string.Join(' ', args)}: {error}");
    }

    public void Dispose() => _cli.Dispose();
}
