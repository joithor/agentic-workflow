using Aw.BugFix.Core.Ports;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class ProcessCheckRunnerContractTests : CheckRunnerContract, IDisposable
{
    private readonly TempDirectory _temp = new();

    protected override ICheckRunner Runner { get; } = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1)), new LocalFiles());

    protected override string Directory => _temp.Path;

    protected override ICheckRunner HangingRunner { get; } = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1)), new LocalFiles());

    protected override IReadOnlyList<string> HangingCommand => ["sleep", "5"];

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_log_holds_the_output_and_a_hang_is_killed_and_fails()
    {
        var log = Path.Combine(_temp.Path, "echo.log");
        Assert.Equal((3, true), Runner.Run(["sh", "-c", "echo out; echo err >&2; exit 3"], Directory, log, Key) is var e ? (e.Code, e.Ran) : default);
        Assert.Equal(["err", "out"], File.ReadAllLines(log).Order(StringComparer.Ordinal), StringComparer.Ordinal);   // two streams, one log: their order isn't fixed

        var quick = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1)), new LocalFiles());
        Assert.False(quick.Run(["sleep", "5"], Directory, Path.Combine(_temp.Path, "sleep.log"), Key).Ran);
    }

    [Fact]
    public void A_background_child_holding_the_output_keeps_the_exit_code_and_does_not_stretch_the_run()
    {
        var runner = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1)), new LocalFiles());
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var exit = runner.Run(["sh", "-c", "sleep 30 & echo started; exit 1"], Directory, Path.Combine(_temp.Path, "bg.log"), Key);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"took {clock.Elapsed}");
        Assert.Equal((1, true), (exit.Code, exit.Ran));
        Assert.Contains("left processes holding its output", File.ReadAllText(Path.Combine(_temp.Path, "bg.log")), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_allowed_variables_reach_the_check()
    {
        var log = Path.Combine(_temp.Path, "env.log");
        string[] shellOwn = ["PATH", "HOME", "PWD", "OLDPWD", "SHLVL", "_"];
        var inherited = Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal)
            .FirstOrDefault(n => !shellOwn.Contains(n, StringComparer.Ordinal) && !CheckEnvironment.Frozen.Contains(n, StringComparer.Ordinal) && !CheckEnvironment.PassedThrough.Contains(n, StringComparer.Ordinal));
        if (inherited is null)
        {
            Assert.Skip("this process has no variable outside the allowlist to probe with");
        }

        // A variable this process has and the settings don't allow: the check must not see it.
        var exit = Runner.Run(["sh", "-c", "printenv \"$1\" || echo unset; echo \"${HOME:+home}\"", "sh", inherited], Directory, log, Key);

        Assert.True(exit.Ran);
        Assert.Equal(["unset", "home"], File.ReadAllLines(log), StringComparer.Ordinal);
    }

    [Fact]
    public void The_frozen_variables_are_kept_as_digests_never_as_values()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";
        var runner = new ProcessCheckRunner(
            BugFixSettings.Read(name => name switch { "PATH" => path, "TZ" => "UTC", "DATABASE_URL" => "postgres://u:" + "secret@db/x", "AW_BUGFIX_CHECK_ENV" => "TZ,DATABASE_URL", _ => null }),
            new LocalFiles());

        var exit = runner.Run(["sh", "-c", "echo \"$TZ $DATABASE_URL\""], Directory, Path.Combine(_temp.Path, "digest.log"), Key);   // TZ is passed through and frozen: no duplicate

        Assert.Equal("UTC postgres://u:" + "secret@db/x", File.ReadAllText(Path.Combine(_temp.Path, "digest.log")).Trim());
        Assert.Equal(["DATABASE_URL", "PATH", "TZ"], exit.Environment.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(exit.Environment.Values, v => Assert.Matches("^hmac-sha256:[0-9a-f]{64}$", v));
        Assert.DoesNotContain(exit.Environment.Values, v => v.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void A_relative_program_starts_from_the_runs_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = Path.Combine(_temp.Path, "check.sh");
        _ = ExecutableScript.Write(script, "exit 4\n");

        var exit = Runner.Run(["./check.sh"], Directory, Path.Combine(_temp.Path, "rel.log"), Key);

        Assert.Equal((4, true), (exit.Code, exit.Ran));
        Assert.Equal(new LocalFiles().Sha256(script).Value, exit.ProgramSha256);
    }

    [Fact]
    public void A_symlinked_program_runs_under_the_name_it_was_found_by()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var target = Path.Combine(_temp.Path, "dispatch.sh");
        _ = ExecutableScript.Write(target, "[ \"$(basename \"$0\")\" = alias ] && exit 0 || exit 5\n");
        _ = File.CreateSymbolicLink(Path.Combine(_temp.Path, "alias"), target);

        var exit = Runner.Run(["./alias"], Directory, Path.Combine(_temp.Path, "alias.log"), Key);

        Assert.Equal((0, true), (exit.Code, exit.Ran));   // a venv's python or bunx keeps its name
        Assert.Equal(new LocalFiles().Sha256(target).Value, exit.ProgramSha256);
    }
}
