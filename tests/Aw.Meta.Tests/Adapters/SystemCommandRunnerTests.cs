using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Commands;
using Aw.Meta.Infrastructure.Processes;

namespace Aw.Meta.Tests.Adapters;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SystemCommandRunnerTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    [Fact, Trait("Lane", "integration")]
    public async Task The_working_directory_and_environment_are_honored()
    {
        var request = new CommandRequest(["sh", "-c", "pwd; printf %s \"$META_TEST\""], TimeSpan.FromSeconds(20))
        {
            Cwd = _dir.Path,
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["META_TEST"] = "value-1" },
        };

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(Path.GetFileName(_dir.Path), result.Value.Stdout, StringComparison.Ordinal);
        Assert.EndsWith("value-1", result.Value.Stdout, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Variables_named_to_unset_do_not_reach_the_command()
    {
        Environment.SetEnvironmentVariable("AW_META_TEST_LEAK", "leaked");
        try
        {
            var request = new CommandRequest(["sh", "-c", "printf %s \"${AW_META_TEST_LEAK-unset}\""], TimeSpan.FromSeconds(20)) { UnsetEnv = ["AW_META_TEST_LEAK"] };

            var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

            Assert.Equal("unset", result.Value.Stdout);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AW_META_TEST_LEAK", value: null);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_isolated_command_gets_a_fresh_home_and_state_directory_that_are_gone_afterwards()
    {
        Environment.SetEnvironmentVariable("AW_META_TEST_TMUX_LIKE", "leaked");
        try
        {
            var script = "printf '%s\\n%s\\n%s\\n' \"$HOME\" \"$AW_STATE_DIR\" \"${AW_META_TEST_TMUX_LIKE-unset}\"; test -d \"$HOME\" && test -d \"$AW_STATE_DIR\" && echo dirs-exist";
            var request = new CommandRequest(["sh", "-c", script], TimeSpan.FromSeconds(20)) { Isolated = true, UnsetEnv = ["AW_META_TEST_TMUX_LIKE"] };
            var realHome = Environment.GetEnvironmentVariable("HOME");

            var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

            var lines = result.Value.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.NotEqual(realHome, lines[0], StringComparer.Ordinal);
            Assert.StartsWith(Path.GetTempPath(), lines[0], StringComparison.Ordinal);
            Assert.NotEqual(lines[0], lines[1], StringComparer.Ordinal);
            Assert.Equal(["unset", "dirs-exist"], lines[2..]);
            Assert.False(Directory.Exists(lines[0]));
            Assert.False(Directory.Exists(lines[1]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AW_META_TEST_TMUX_LIKE", value: null);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Isolation_can_start_after_a_wrapper_which_keeps_the_real_environment()
    {
        var script = "echo \"wrapper:$HOME\"; exec \"$@\"";
        var request = new CommandRequest(["sh", "-c", script, "sh", "sh", "-c", "echo \"command:$HOME\""], TimeSpan.FromSeconds(20)) { Isolated = true, IsolatedFrom = 4 };
        var realHome = Environment.GetEnvironmentVariable("HOME");

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        var lines = result.Value.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("wrapper:" + realHome, lines[0]);
        Assert.StartsWith("command:" + Path.GetTempPath(), lines[1], StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_relative_script_runs_from_the_working_directory_whatever_ours_is()
    {
        var scripts = Directory.CreateDirectory(Path.Combine(_dir.Path, "scripts")).FullName;
        var script = Path.Combine(scripts, "mine.sh");
        _ = ExecutableScript.Write(script, "echo from-the-tree\n");
        var request = new CommandRequest(["scripts/mine.sh"], TimeSpan.FromSeconds(20)) { Cwd = _dir.Path };

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsSuccess ? "" : result.Error.ToString());
        Assert.Contains("from-the-tree", result.Value.Stdout, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Both_streams_are_captured_and_appended_to_the_log()
    {
        var log = Path.Combine(_dir.Path, "out", "step.log");
        var request = new CommandRequest(["sh", "-c", "echo to-out; echo to-err 1>&2"], TimeSpan.FromSeconds(20)) { LogFile = log };

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        Assert.Contains("to-out", result.Value.Stdout, StringComparison.Ordinal);
        Assert.Contains("to-err", result.Value.Stderr, StringComparison.Ordinal);
        var text = await File.ReadAllTextAsync(log, TestContext.Current.CancellationToken);
        Assert.Contains("to-out", text, StringComparison.Ordinal);
        Assert.Contains("to-err", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Cancelling_kills_the_command_and_surfaces_the_cancellation()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var request = new CommandRequest(["sleep", "60"], TimeSpan.FromSeconds(60));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new SystemCommandRunner().Run(request, cancel.Token));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Cancelling_gives_the_command_a_SIGTERM_first_so_its_exit_trap_runs()
    {
        var marker = Path.Combine(_dir.Path, "trapped");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
        var request = new CommandRequest(["bash", "-c", "trap 'echo term > \"$1\"; exit 0' TERM; sleep 60 & wait", "bash", marker], TimeSpan.FromSeconds(60));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new SystemCommandRunner().Run(request, cancel.Token));

        Assert.True(File.Exists(marker));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_command_that_ignores_SIGTERM_is_killed_after_its_grace_with_what_it_started()
    {
        var pidFile = Path.Combine(_dir.Path, "child.pid");
        using var cancel = new CancellationTokenSource();
        var request = new CommandRequest(["bash", "-c", "trap '' TERM; sleep 60 & echo $! > \"$1\"; wait", "bash", pidFile], TimeSpan.FromSeconds(60)) { TermGrace = TimeSpan.FromMilliseconds(200) };
        var running = new SystemCommandRunner().Run(request, cancel.Token).AsTask();
        var child = ProcessTree.Snapshot(await WaitForPid(pidFile))[0];
        var watch = Stopwatch.StartNew();

        await cancel.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), "took " + watch.Elapsed.ToString());
        await AssertGone(child);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_descendant_that_ignores_SIGTERM_is_killed_after_the_grace_even_when_the_root_exits_on_it()
    {
        var pidFile = Path.Combine(_dir.Path, "survivor.pid");
        using var cancel = new CancellationTokenSource();
        var request = new CommandRequest(["bash", "-c", "(trap '' TERM; sleep 60) & echo $! > \"$1\"; wait", "bash", pidFile], TimeSpan.FromSeconds(60)) { TermGrace = TimeSpan.FromMilliseconds(200) };
        var running = new SystemCommandRunner().Run(request, cancel.Token).AsTask();
        var survivor = ProcessTree.Snapshot(await WaitForPid(pidFile))[0];

        await cancel.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));

        await AssertGone(survivor);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_grandchild_gets_the_SIGTERM_too_so_its_own_trap_runs()
    {
        var marker = Path.Combine(_dir.Path, "grandchild-trapped");
        var ready = Path.Combine(_dir.Path, "grandchild-ready");
        using var cancel = new CancellationTokenSource();
        var inner = "trap 'echo term > \"$1\"; exit 0' TERM; sleep 60 & touch \"$2\"; wait";
        var request = new CommandRequest(["bash", "-c", "bash -c \"$1\" bash \"$2\" \"$3\" & wait", "bash", inner, marker, ready], TimeSpan.FromSeconds(60));
        var running = new SystemCommandRunner().Run(request, cancel.Token).AsTask();
        await WaitForFile(ready);

        await cancel.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await running.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken));

        Assert.True(File.Exists(marker));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_process_tree_has_a_started_child_below_the_parent_and_not_the_parent_below_the_child()
    {
        var info = new ProcessStartInfo("sleep", ["30"]) { UseShellExecute = false };
        using var child = Process.Start(info) ?? throw new InvalidOperationException("sleep did not start");
        try
        {
            var fromParent = ProcessTree.Of(Environment.ProcessId);
            var fromChild = ProcessTree.Of(child.Id);

            Assert.Equal(Environment.ProcessId, fromParent[0]);
            Assert.Contains(child.Id, fromParent);
            Assert.Equal([child.Id], fromChild);
            Assert.DoesNotContain(Environment.ProcessId, fromChild);
            Assert.True(ProcessTree.IsAlive(ProcessTree.Snapshot(child.Id)[0]));
            Assert.False(ProcessTree.IsAlive(new ProcessRef(child.Id, "0")));   // same pid, other start time: a reused pid is not the process
        }
        finally
        {
            child.Kill();
            await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }

    private static async Task<int> WaitForPid(string file)
    {
        await WaitForFile(file);
        for (var i = 0; i < 100; i++)
        {
            if (int.TryParse((await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).Trim(), out var pid))
            {
                return pid;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException(file);
    }

    private static async Task WaitForFile(string file)
    {
        for (var i = 0; i < 200 && !File.Exists(file); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(file), file);
    }

    private static async Task AssertGone(ProcessRef process)
    {
        for (var i = 0; i < 100 && ProcessTree.IsAlive(process); i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        Assert.False(ProcessTree.IsAlive(process), "the process still runs");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_log_that_cannot_be_opened_is_a_start_failure_and_nothing_is_started()
    {
        var blocker = Path.Combine(_dir.Path, "file");
        await File.WriteAllTextAsync(blocker, "x", TestContext.Current.CancellationToken);
        var started = Path.Combine(_dir.Path, "started");
        var request = new CommandRequest(["touch", started], TimeSpan.FromSeconds(5)) { LogFile = Path.Combine(blocker, "step.log") };

        var result = await new SystemCommandRunner().Run(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.False(File.Exists(started));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_grandchild_holding_the_pipes_open_does_not_hang_the_run_or_fail_it_afterwards()
    {
        var unobserved = new List<Exception>();
        void Record(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            lock (unobserved)
            {
                if (e.Exception.ToString().Contains(nameof(SystemCommandRunner), StringComparison.Ordinal))
                {
                    unobserved.Add(e.Exception);   // only this runner's: the handler is process-wide
                }
            }
        }

        var pidFile = Path.Combine(_dir.Path, "grandchild.pid");
        TaskScheduler.UnobservedTaskException += Record;
        try
        {
            // The grandchild inherits stdout and stderr and holds them 30 s: the run must end without it.
            var script = $"sh -c 'echo $$ > \"{pidFile}\"; exec sleep 30' & echo early";
            var request = new CommandRequest(["sh", "-c", script], TimeSpan.FromSeconds(20)) { LogFile = Path.Combine(_dir.Path, "late.log") };

            var run = new SystemCommandRunner(TimeSpan.FromMilliseconds(100)).Run(request, CancellationToken.None).AsTask();
            var finished = await Task.WhenAny(run, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Same(run, finished);
            var result = await run;
            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Assert.Contains("early", result.Value.Stdout, StringComparison.Ordinal);
            lock (unobserved)
            {
                Assert.Empty(unobserved);
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Record;
            KillGrandchild(pidFile);
        }
    }

    // By PID only: the sleeping grandchild outlives its parent shell, so the test ends it itself.
    private static void KillGrandchild(string pidFile)
    {
        if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture, out var pid))
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                process.Kill();
            }
            catch (ArgumentException)
            {
                // already gone
            }
            catch (InvalidOperationException)
            {
                // already gone
            }
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_missing_working_directory_is_a_start_failure()
    {
        var request = new CommandRequest(["true"], TimeSpan.FromSeconds(5)) { Cwd = Path.Combine(_dir.Path, "absent") };

        var result = await new SystemCommandRunner().Run(request, CancellationToken.None);

        Assert.True(result.IsError);
    }

    public void Dispose() => _dir.Dispose();
}
