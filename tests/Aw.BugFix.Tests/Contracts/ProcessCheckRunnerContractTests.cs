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
        Assert.Equal(3, Runner.Run(["sh", "-c", "echo out; echo err >&2; exit 3"], Directory, log));
        Assert.Equal("out\nerr\n", File.ReadAllText(log));

        var quick = new ProcessCheckRunner(new BugFixSettings(TimeSpan.FromMilliseconds(300)));
        Assert.NotEqual(0, quick.Run(["sleep", "5"], Directory, Path.Combine(_temp.Path, "sleep.log")));
    }
}
