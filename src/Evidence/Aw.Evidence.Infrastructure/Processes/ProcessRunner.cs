using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Processes;

/// <summary>Runs a command to completion (argument list, never a shell), with optional standard input and a time limit.</summary>
internal static class ProcessRunner
{
    /// <summary>Runs <paramref name="command"/>.</summary>
    /// <param name="command">The program, then its arguments.</param>
    /// <param name="standardInput">Text to write to its standard input, or null for none.</param>
    /// <param name="timeout">How long it may run before it is killed.</param>
    /// <param name="failure">The error to report when it can't start or runs out of time.</param>
    /// <param name="cancellationToken">Cancels the run (the process is killed).</param>
    /// <returns>The outcome, or <paramref name="failure"/> saying what went wrong.</returns>
    internal static ValueTask<Result<ProcessOutcome>> RunAsync(
        IReadOnlyList<string> command, string? standardInput, TimeSpan timeout, ErrorDefinition failure, CancellationToken cancellationToken) =>
        RunAsync(new ProcessSpec(command, standardInput, Environment: null, WorkingDirectory: null), timeout, failure, cancellationToken);

    /// <summary>Runs <paramref name="spec"/>: like the overload above, with an exact environment and a working directory when the spec gives them.</summary>
    /// <param name="spec">The program, its arguments, input, environment and working directory.</param>
    /// <param name="timeout">How long it may run before it is killed.</param>
    /// <param name="failure">The error to report when it can't start or runs out of time.</param>
    /// <param name="cancellationToken">Cancels the run (the process is killed).</param>
    /// <returns>The outcome, or <paramref name="failure"/> saying what went wrong.</returns>
    internal static async ValueTask<Result<ProcessOutcome>> RunAsync(ProcessSpec spec, TimeSpan timeout, ErrorDefinition failure, CancellationToken cancellationToken)
    {
        var (command, standardInput) = (spec.Command, spec.StandardInput);
        using var process = new Process { StartInfo = StartInfo(spec) };
        try
        {
            _ = process.Start();
        }
        catch (Win32Exception ex)
        {
            return failure.With($"{command[0]} could not start: {ex.Message}");
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            return await CollectAsync(process, standardInput, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill(process);   // on a timeout and on Ctrl-C alike: never leave docker exec / psql behind
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return failure.With(string.Create(CultureInfo.InvariantCulture, $"{command[0]} did not finish within {timeout.TotalSeconds:0} s"));
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
            // It already exited.
        }
    }

    private static async Task<ProcessOutcome> CollectAsync(Process process, string? standardInput, CancellationToken cancellationToken)
    {
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    private static ProcessStartInfo StartInfo(ProcessSpec spec)
    {
        var start = new ProcessStartInfo(spec.Command[0])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = spec.StandardInput is not null,
            UseShellExecute = false,
        };
        foreach (var argument in spec.Command.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        if (spec.WorkingDirectory is not null)
        {
            start.WorkingDirectory = spec.WorkingDirectory;
        }

        if (spec.Environment is not null)
        {
            start.Environment.Clear();
            foreach (var (name, value) in spec.Environment)
            {
                start.Environment[name] = value;
            }
        }

        return start;
    }
}
