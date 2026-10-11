using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Aw.BugFix.Infrastructure;

/// <summary>Runs a program to completion within a time limit, capturing its output or streaming it to a log.</summary>
internal static class Processes
{
    /// <summary>The exit code reported for a program that couldn't start.</summary>
    internal const int CouldNotStart = 127;

    /// <summary>The exit code reported for a program killed at its time limit.</summary>
    internal const int TimedOut = 124;

    /// <summary>The most output kept or logged per run; the rest is dropped and the log says so.</summary>
    internal const int OutputCap = 16 * 1024 * 1024;

    internal static ProcessResult Capture(ProcessStartInfo start, TimeSpan timeout)
    {
        using var output = new MemoryStream();
        using var error = new MemoryStream();
        var (code, note) = Run(start, timeout, output, error);
        return new ProcessResult(code, Encoding.UTF8.GetString(output.ToArray()), note ?? Encoding.UTF8.GetString(error.ToArray()));
    }

    /// <summary>Runs with stdout and stderr appended to <paramref name="log"/>; Ran is false when it couldn't start or hit the time limit.</summary>
    internal static (int Code, bool Ran) ToLog(ProcessStartInfo start, TimeSpan timeout, string log)
    {
        using var file = new FileStream(log, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var sink = Stream.Synchronized(file);
        var (code, note) = Run(start, timeout, sink, sink);
        if (note is not null)
        {
            sink.Write(Encoding.UTF8.GetBytes($"\nbugfix-state: {note}\n"));
        }

        return (code, note is null);
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

    // The deadline covers the process and its output: a background child that keeps the pipes open can't stretch it.
    private static (int Code, string? Note) Run(ProcessStartInfo start, TimeSpan timeout, Stream output, Stream error)
    {
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.RedirectStandardInput = true;
        start.UseShellExecute = false;
        Process process;
        try
        {
            process = Process.Start(start)!; // why: Start returns null only when UseShellExecute reuses a process; it is off.
        }
        catch (Win32Exception ex)
        {
            return (CouldNotStart, $"{start.FileName} can't run: {ex.Message}");
        }

        using (process)
        {
            process.StandardInput.Close();
            var clock = Stopwatch.StartNew();
            var pumps = Task.WhenAll(
                Pump(process.StandardOutput.BaseStream, output),
                Pump(process.StandardError.BaseStream, error));
            var exited = process.WaitForExit(timeout);
            var left = timeout - clock.Elapsed;
            if (exited && pumps.Wait(left > TimeSpan.Zero ? left : TimeSpan.Zero))
            {
                return (process.ExitCode, null);
            }

            Kill(process);
            return (TimedOut, exited
                ? $"{start.FileName} left processes holding its output past the {timeout} limit; counted as not run"
                : $"{start.FileName} took longer than {timeout}; killed and counted as not run");
        }
    }

    private static async Task Pump(Stream from, Stream to)
    {
        var buffer = new byte[81920];
        var written = 0L;
        int read;
        while ((read = await from.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            var keep = (int)Math.Min(read, Math.Max(0, OutputCap - written));
            if (keep > 0)
            {
                await to.WriteAsync(buffer.AsMemory(0, keep)).ConfigureAwait(false);
                written += keep;
            }
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // already exited: its descendants hold the pipes, and nothing more can be done from here.
        }
    }
}
