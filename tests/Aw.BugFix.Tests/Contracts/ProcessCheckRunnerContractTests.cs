using Aw.BugFix.Core.Ports;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class ProcessCheckRunnerContractTests : CheckRunnerContract, IDisposable
{
    private readonly TempDirectory _temp = new();

    protected override ICheckRunner Runner { get; } = new ProcessCheckRunner(new BugFixSettings(TimeSpan.FromSeconds(30)));

    protected override string Directory => _temp.Path;

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_log_holds_the_output_and_a_hang_is_killed_and_fails()
    {
        var log = Path.Combine(_temp.Path, "echo.log");
        Assert.Equal(new Core.Model.CheckExit(3, Ran: true), Runner.Run(["sh", "-c", "echo out; echo err >&2; exit 3"], Directory, log));
        Assert.Equal(["err", "out"], File.ReadAllLines(log).Order(StringComparer.Ordinal), StringComparer.Ordinal);   // two streams, one log: their order isn't fixed

        var quick = new ProcessCheckRunner(new BugFixSettings(TimeSpan.FromMilliseconds(300)));
        Assert.False(quick.Run(["sleep", "5"], Directory, Path.Combine(_temp.Path, "sleep.log")).Ran);
    }

    [Fact]
    public void A_background_child_holding_the_output_cannot_stretch_the_time_limit()
    {
        var quick = new ProcessCheckRunner(new BugFixSettings(TimeSpan.FromSeconds(1)));
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var exit = quick.Run(["sh", "-c", "sleep 30 & echo started; exit 1"], Directory, Path.Combine(_temp.Path, "bg.log"));

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"took {clock.Elapsed}");
        Assert.False(exit.Ran);
        Assert.Contains("left processes holding its output", File.ReadAllText(Path.Combine(_temp.Path, "bg.log")), StringComparison.Ordinal);
    }
}
