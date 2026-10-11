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
        Assert.Equal((3, true), Runner.Run(["sh", "-c", "echo out; echo err >&2; exit 3"], Directory, log) is var e ? (e.Code, e.Ran) : default);
        Assert.Equal(["err", "out"], File.ReadAllLines(log).Order(StringComparer.Ordinal), StringComparer.Ordinal);   // two streams, one log: their order isn't fixed

        var quick = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1)), new LocalFiles());
        Assert.False(quick.Run(["sleep", "5"], Directory, Path.Combine(_temp.Path, "sleep.log")).Ran);
    }

    [Fact]
    public void A_background_child_holding_the_output_keeps_the_exit_code_and_does_not_stretch_the_run()
    {
        var runner = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1)), new LocalFiles());
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var exit = runner.Run(["sh", "-c", "sleep 30 & echo started; exit 1"], Directory, Path.Combine(_temp.Path, "bg.log"));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"took {clock.Elapsed}");
        Assert.Equal((1, true), (exit.Code, exit.Ran));
        Assert.Contains("left processes holding its output", File.ReadAllText(Path.Combine(_temp.Path, "bg.log")), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_allowed_variables_reach_the_check()
    {
        Environment.SetEnvironmentVariable("AW_BUGFIX_TEST_LEAK", "leaked");
        var log = Path.Combine(_temp.Path, "env.log");

        var exit = Runner.Run(["sh", "-c", "echo \"${AW_BUGFIX_TEST_LEAK:-unset} ${HOME:+home}\""], Directory, log);

        Assert.True(exit.Ran);
        Assert.Equal("unset home", File.ReadAllText(log).Trim());
    }

    [Fact]
    public void A_relative_program_starts_from_the_runs_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = Path.Combine(_temp.Path, "check.sh");
        File.WriteAllText(script, "#!/bin/sh\nexit 4\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var exit = Runner.Run(["./check.sh"], Directory, Path.Combine(_temp.Path, "rel.log"));

        Assert.Equal((4, true), (exit.Code, exit.Ran));
        Assert.Equal(new LocalFiles().Sha256(script).Value, exit.ProgramSha256);
    }
}
