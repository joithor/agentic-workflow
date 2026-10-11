using System.Diagnostics;
using System.Globalization;
using Aw.Judge.Core.Processes;
using Aw.Judge.Infrastructure.Processes;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

[Lane(TestLane.Integration)]
public sealed class SystemProcessRunnerTests : IDisposable
{
    private readonly ShellPrograms _bin = new();

    public void Dispose() => _bin.Dispose();

    [Fact]
    public async Task A_timeout_stops_the_processes_the_program_started_too()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        var pidFile = Path.Combine(_bin.Directory, "child.pid");
        _ = _bin.InstallScript("spawner", $"sleep 30 &\necho $! > {ShellPrograms.Quote(pidFile)}\nwait\n");
        var clock = Stopwatch.StartNew();

        var result = await new SystemProcessRunner(_bin.Environment).Run(
            new ProcessRequest { FileName = "spawner", Timeout = TimeSpan.FromMilliseconds(500) }, TestContext.Current.CancellationToken);

        Assert.True(result.TimedOut);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"stopped near the timeout, took {clock.Elapsed}");
        var child = int.Parse((await File.ReadAllTextAsync(pidFile, TestContext.Current.CancellationToken)).Trim(), CultureInfo.InvariantCulture);
        Assert.True(await Gone(child), $"the background child {child.ToString(CultureInfo.InvariantCulture)} was stopped too");
    }

    [Fact]
    public async Task A_path_is_run_as_is_without_a_search()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        var file = _bin.InstallScript("direct", "printf ok\n");

        var result = await new SystemProcessRunner(_ => null).Run(
            new ProcessRequest { FileName = file, Timeout = TimeSpan.FromSeconds(10) }, TestContext.Current.CancellationToken);

        Assert.Equal(ProcessResult.Exited(0, "ok", ""), result);
    }

    [Fact]
    public async Task Cancelling_stops_the_program_and_throws()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the program is an sh script");
        _ = _bin.InstallScript("sleeper", "sleep 30\n");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await new SystemProcessRunner(_bin.Environment).Run(new ProcessRequest { FileName = "sleeper", Timeout = TimeSpan.FromSeconds(30) }, cancel.Token));
    }

    private static async Task<bool> Gone(int pid)
    {
        for (var i = 0; i < 40; i++)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return false;
    }
}
