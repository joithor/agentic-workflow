using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Aw.BugFix.Infrastructure;

/// <summary>Runs a program to completion, capturing or forwarding its output, with a time limit.</summary>
internal static class Processes
{
    /// <summary>The exit code reported for a program that couldn't start.</summary>
    internal const int CouldNotStart = 127;

    /// <summary>The exit code reported for a program killed at its time limit.</summary>
    internal const int TimedOut = 124;

    internal static ProcessResult Capture(ProcessStartInfo start, TimeSpan timeout)
    {
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.UseShellExecute = false;
        start.StandardOutputEncoding = Encoding.UTF8;
        start.StandardErrorEncoding = Encoding.UTF8;
        Process process;
        try
        {
            process = Process.Start(start)!; // why: Start returns null only when UseShellExecute reuses a process; it is off.
        }
        catch (Win32Exception ex)
        {
            return new ProcessResult(CouldNotStart, string.Empty, $"{start.FileName} can't run: {ex.Message}");
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return new ProcessResult(TimedOut, output.Result, $"{start.FileName} took longer than {timeout}");
            }

            return new ProcessResult(process.ExitCode, output.Result, error.Result);
        }
    }

    internal static ProcessStartInfo Start(string program, IEnumerable<string> args, string? directory = null)
    {
        var start = new ProcessStartInfo(program) { WorkingDirectory = directory ?? string.Empty };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        return start;
    }
}
