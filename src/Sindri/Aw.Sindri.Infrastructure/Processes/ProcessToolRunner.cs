using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>IToolRunner over System.Diagnostics.Process: no shell, standard output captured, standard error read line by line (handed to the caller's callback, or dropped).</summary>
internal sealed class ProcessToolRunner : IToolRunner
{
    /// <inheritdoc />
    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, Action<string>? standardErrorLine = null, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ToolRun.Cancelled;
        }

        var start = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return ToolRun.Missing;   // not on PATH
        }

        if (process is null)
        {
            return ToolRun.Missing;
        }

        using (process)
        {
            return Wait(process, timeout, standardErrorLine, cancellationToken);
        }
    }

    private static ToolRun Wait(Process process, TimeSpan timeout, Action<string>? standardErrorLine, CancellationToken cancellationToken)
    {
        process.StandardInput.Close();   // a tool that prompts reads end of input instead of waiting for a terminal
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = DrainLines(process.StandardError, standardErrorLine);
        using var registration = cancellationToken.Register(() => Kill(process));
        if (!process.WaitForExit(timeout))
        {
            Kill(process);
            process.WaitForExit();
            return ToolRun.TimedOut;
        }

        process.WaitForExit();   // flushes the redirected streams
        error.GetAwaiter().GetResult();
        var text = output.GetAwaiter().GetResult();
        return cancellationToken.IsCancellationRequested ? ToolRun.Cancelled : new ToolRun(ToolOutcome.Exited, process.ExitCode, text);
    }

    private static async Task DrainLines(StreamReader reader, Action<string>? onLine)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            onLine?.Invoke(line);
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
            // it already exited
        }
    }
}
