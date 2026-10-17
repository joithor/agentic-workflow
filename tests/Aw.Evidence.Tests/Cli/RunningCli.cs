using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Cli;

/// <summary>A running ui-evidence: its stdout and stderr read as they arrive, a signal sender, and a bounded wait.</summary>
internal sealed class RunningCli : IDisposable
{
    private readonly Process _process;
    private readonly StringBuilder _out = new();
    private readonly StringBuilder _err = new();

    internal RunningCli(Process process)
    {
        _process = process;
        _process.OutputDataReceived += (_, e) => Append(_out, e.Data);
        _process.ErrorDataReceived += (_, e) => Append(_err, e.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    /// <summary>Waits until stderr contains <paramref name="text"/>; fails the test past <see cref="CliWorkspace.Limit"/>.</summary>
    public void WaitForStderr(string text)
    {
        var deadline = Stopwatch.StartNew();
        while (!Snapshot(_err).Contains(text, StringComparison.Ordinal))
        {
            Assert.False(_process.HasExited, $"ui-evidence exited before writing '{text}': {Snapshot(_err)}");
            Assert.True(deadline.Elapsed < CliWorkspace.Limit, $"no '{text}' on stderr within {CliWorkspace.Limit}: {Snapshot(_err)}");
            RealTime.Sleep(TimeSpan.FromMilliseconds(20));
        }
    }

    /// <summary>Sends a POSIX signal (TERM, INT, HUP, …) with kill(1).</summary>
    public void Signal(string name)
    {
        using var kill = Process.Start("kill", ["-s", name, _process.Id.ToString(CultureInfo.InvariantCulture)]);
        kill.WaitForExit();
        Assert.Equal(0, kill.ExitCode);
    }

    /// <summary>Waits for the exit (killing the tree past <see cref="CliWorkspace.Limit"/>) and returns the exit code and output.</summary>
    public (int Exit, string Out, string Err) WaitForExit()
    {
        if (!_process.WaitForExit(CliWorkspace.Limit))
        {
            _process.Kill(entireProcessTree: true);
            Assert.Fail($"ui-evidence ran past {CliWorkspace.Limit} and was killed: {Snapshot(_err)}");
        }

        _process.WaitForExit();   // drains the asynchronous readers
        return (_process.ExitCode, Snapshot(_out), Snapshot(_err));
    }

    public void Dispose()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        _process.Dispose();
    }

    private static void Append(StringBuilder buffer, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (buffer)
        {
            _ = buffer.Append(line).Append('\n');
        }
    }

    private static string Snapshot(StringBuilder buffer)
    {
        lock (buffer)
        {
            return buffer.ToString();
        }
    }
}
