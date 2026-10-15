using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IProcessTable contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class ProcessTableContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>The table under test.</summary>
    /// <returns>The table.</returns>
    protected abstract IProcessTable Create();

    /// <summary>Starts a long-running process the table will list, which it can terminate.</summary>
    /// <returns>Its pid.</returns>
    protected abstract int StartProcess();

    /// <summary>A started process is listed with a command line, and the host figures are real numbers.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_started_process_is_listed_with_host_figures()
    {
        var table = Create();
        var pid = StartProcess();

        var snapshot = await table.Snapshot(_none);

        Expect.That(snapshot.IsSuccess, "the snapshot is read");
        var listed = snapshot.Value.Processes.FirstOrDefault(p => p.Pid == pid);
        Expect.That(listed is not null && listed.Cmdline.Contains("sleep", StringComparison.Ordinal), "the process is listed with its command line");
        Expect.That(snapshot.Value.MemAvailableKb > 0 && snapshot.Value.DiskFreeBytes > 0, "memory and disk are reported");
    }

    /// <summary>Terminating a process removes it from the table.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Terminate_removes_the_process()
    {
        var table = Create();
        var pid = StartProcess();

        var done = await table.Terminate(pid, _none);
        var gone = false;
        for (var attempt = 0; attempt < 50 && !gone; attempt++)
        {
            gone = (await table.Snapshot(_none)).Value.Processes.All(p => p.Pid != pid);
            if (!gone)
            {
                await Task.Delay(100, _none);
            }
        }

        Expect.That(done.IsSuccess, "the signal is sent");
        Expect.That(gone, "the process leaves the table");
    }

    /// <summary>Terminating a pid that does not exist fails.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Terminating_an_unknown_pid_fails()
    {
        var result = await Create().Terminate(2_147_000_000, _none);

        Expect.That(result.IsError && result.Error.Code == MetaErrors.HostUnreadable.Code, "AW-META-010");
    }
}
